//
// VPathToHost.cs
//
// Authors:
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Lluis Sanchez Gual (lluis@ximian.com)
//
// Copyright (c) Copyright 2002-2007 Novell, Inc
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Web.Hosting;
using System.Globalization;
using System.Diagnostics;

namespace Mono.WebServer
{
    public class VPathToHost
    {
        public readonly string vhost;
        public readonly int vport;
        public readonly string vpath;
        public string realPath;
        public readonly bool haveWildcard;
        public IApplicationHost AppHost;
        public IRequestBroker RequestBroker;

        public VPathToHost(string vhost, int vport, string vpath, string realPath)
        {
            this.vhost = (vhost != null) ? vhost.ToLower(CultureInfo.InvariantCulture) : null;
            this.vport = vport;
            this.vpath = vpath;
            if (String.IsNullOrEmpty(vpath) || vpath[0] != '/')
                throw new ArgumentException("Virtual path must begin with '/': " + vpath,
                                 "vpath");

            this.realPath = realPath;
            AppHost = null;
            if (vhost != null && this.vhost.Length != 0 && this.vhost[0] == '*')
            {
                haveWildcard = true;
                if (this.vhost.Length > 2 && this.vhost[1] == '.')
                    this.vhost = this.vhost.Substring(2);
            }
        }

        public bool TryClearHost(IApplicationHost host)
        {
            if (AppHost == host)
            {
                AppHost = null;
                return true;
            }

            return false;
        }

        public void UnloadHost()
        {
            if (AppHost != null)
                AppHost.Unload();

            AppHost = null;
        }

        public bool Redirect(string path, out string redirect)
        {
            redirect = null;
            if (path.Length == vpath.Length - 1)
            {
                redirect = vpath;
                return true;
            }

            return false;
        }

        public bool Match(string vhost, int vport, string vpath)
        {
            if (vport != -1 && this.vport != -1 && vport != this.vport)
                return false;

            if (vpath == null)
                return false;

            if (vhost != null && this.vhost != null && this.vhost != "*")
            {
                int length = this.vhost.Length;
                string lwrvhost = vhost.ToLower(CultureInfo.InvariantCulture);
                if (haveWildcard)
                {
                    if (length > vhost.Length)
                        return false;

                    if (length == vhost.Length && this.vhost != lwrvhost)
                        return false;

                    if (vhost[vhost.Length - length - 1] != '.')
                        return false;

                    if (!lwrvhost.EndsWith(this.vhost))
                        return false;

                }
                else if (this.vhost != lwrvhost)
                {
                    return false;
                }
            }

            int local = vpath.Length;
            int vlength = this.vpath.Length;
            if (vlength > local)
            {
                // Check for /xxx requests to be redirected to /xxx/
                if (this.vpath[vlength - 1] != '/')
                    return false;

                return (vlength - 1 == local && this.vpath.Substring(0, vlength - 1) == vpath);
            }

            return (vpath.StartsWith(this.vpath));
        }

        public void CreateHost(ApplicationServer server, WebSource webSource)
        {
            string v = vpath;
            if (v != "/" && v.EndsWith("/"))
            {
                v = v.Substring(0, v.Length - 1);
            }

            var domain = AppDomain.CurrentDomain;
            var debugDomain = domain.GetData("DebugDomain") as DebugDomain;
            var listAsm = domain.GetAssemblies();
            // { Mono.WebServer, Version = 4.4.0.0, Culture = neutral, PublicKeyToken = 0738eb9f132ed756}
            // { Mono.WebServer.XSP, Version = 4.9.0.0, Culture = neutral, PublicKeyToken = 0738eb9f132ed756}

            Exception error = null;
            try
            {
                var hostType = webSource.GetApplicationHostType();
                AppHost =
                    Mono.Web.Hosting.
                    ApplicationHost.CreateApplicationHost(hostType, v, realPath) as IApplicationHost;
                AppHost.Server = server;
            }
            catch (Exception err) { error = err.InnerException ?? err; }

            if (AppHost == null && error != null)
            {
                Console.WriteLine(error.Message);
                if (Debugger.IsAttached)
                    Debugger.Break();

                Console.ReadKey();
                throw error;
            }

            if (!server.SingleApplication)
            {
                // Link the host in the application domain with a request broker in the main domain
                RequestBroker = webSource.CreateRequestBroker();
                AppHost.RequestBroker = RequestBroker;
            }
        }
    }
}

namespace Mono.Web.Hosting
{
    using System.Web;
    using System.Security.Permissions;
    using System.IO;
    using System.Security.Policy;
    using System.Web.Configuration;


    // https://github.com/mono/mono/blob/master/mcs/class/System.Web/System.Web.Hosting/ApplicationHost.cs
    // CAS - no InheritanceDemand here as the class is sealed
    [AspNetHostingPermission(SecurityAction.LinkDemand, Level = AspNetHostingPermissionLevel.Minimal)]
    public sealed class ApplicationHost
    {
        #region Prepare

        const string DEFAULT_WEB_CONFIG_NAME = "web.config";
        internal const string MonoHostedDataKey = ".:!MonoAspNetHostedApp!:.";

        static object create_dir = new object();

        ApplicationHost()
        {
        }

        internal static string FindWebConfig(string basedir)
        {
            if (String.IsNullOrEmpty(basedir) || !Directory.Exists(basedir))
                return null;

            string[] files = Directory.GetFileSystemEntries(basedir, "?eb.?onfig");
            if (files == null || files.Length == 0)
                return null;
            return files[0];
        }

        internal static bool ClearDynamicBaseDirectory(string directory)
        {
            string[] entries = null;

            try
            {
                entries = Directory.GetDirectories(directory);
            }
            catch
            {
                // ignore
            }

            bool dirEmpty = true;
            if (entries != null && entries.Length > 0)
            {
                foreach (string e in entries)
                {
                    if (ClearDynamicBaseDirectory(e))
                    {
                        try
                        {
                            Directory.Delete(e);
                        }
                        catch
                        {
                            dirEmpty = false;
                        }
                    }
                }
            }

            try
            {
                entries = Directory.GetFiles(directory);
            }
            catch
            {
                entries = null;
            }

            if (entries != null && entries.Length > 0)
            {
                foreach (string e in entries)
                {
                    try
                    {
                        File.Delete(e);
                    }
                    catch
                    {
                        dirEmpty = false;
                    }
                }
            }

            return dirEmpty;
        }

        static bool CreateDirectory(string directory)
        {
            lock (create_dir)
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    return false;
                }
                else
                    return true;
            }
        }

        static string BuildPrivateBinPath(string physicalPath, string[] dirs)
        {
            int len = dirs.Length;
            string[] ret = new string[len];
            for (int i = 0; i < len; i++)
                ret[i] = Path.Combine(physicalPath, dirs[i]);
            return String.Join(";", ret);
        }

        #endregion

        // For further details see `Hosting the ASP.NET runtime'
        //
        //    http://www.west-wind.com/presentations/aspnetruntime/aspnetruntime.asp
        // 
        [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
        public static object CreateApplicationHost(Type hostType, string virtualDir, string physicalDir)
        {
            if (physicalDir == null)
                throw new NullReferenceException();

            #region Prepare bin

            // Make sure physicalDir has file system semantics
            // and not uri semantics ( '\' and not '/' ).
            physicalDir = Path.GetFullPath(physicalDir);

            if (hostType == null)
                throw new ArgumentException("hostType can't be null");

            if (virtualDir == null)
                throw new ArgumentNullException("virtualDir");

            Evidence evidence = new Evidence(AppDomain.CurrentDomain.Evidence);

            //
            // Setup
            //
            AppDomainSetup setup = new AppDomainSetup();

            setup.ApplicationBase = physicalDir;

            string webConfig = FindWebConfig(physicalDir);

            if (webConfig == null)
                webConfig = Path.Combine(physicalDir, DEFAULT_WEB_CONFIG_NAME);
            setup.ConfigurationFile = webConfig;
            setup.DisallowCodeDownload = true;

            string[] bindirPath = new string[1] { Path.Combine(physicalDir, "bin") };

            //string bindir;
            //foreach (string dir in HttpApplication.BinDirs)
            //{
            //    bindir = Path.Combine(physicalDir, dir);

            //    if (Directory.Exists(bindir))
            //    {
            //        bindirPath[0] = bindir;
            //        break;
            //    }
            //}

            setup.PrivateBinPath = BuildPrivateBinPath(physicalDir, bindirPath);
            setup.PrivateBinPathProbe = "*";
            string dynamic_dir = null;
            string user = Environment.UserName;
            int tempDirTag = 0;
            string dirPrefix = String.Concat(user, "-temp-aspnet-");

            for (int i = 0; ; i++)
            {
                string d = Path.Combine(Path.GetTempPath(), String.Concat(dirPrefix, i.ToString("x")));

                try
                {
                    CreateDirectory(d);
                    string stamp = Path.Combine(d, "stamp");
                    CreateDirectory(stamp);
                    dynamic_dir = d;
                    try
                    {
                        Directory.Delete(stamp);
                    }
                    catch (Exception)
                    {
                        // ignore
                    }

                    tempDirTag = i.GetHashCode();
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
            }
            // 
            // Unique Domain ID
            //
            string domain_id = (virtualDir.GetHashCode() + 1 ^ physicalDir.GetHashCode() + 2 ^ tempDirTag).ToString("x");

            // This is used by mod_mono's fail-over support
            string domain_id_suffix = Environment.GetEnvironmentVariable("__MONO_DOMAIN_ID_SUFFIX");
            if (domain_id_suffix != null && domain_id_suffix.Length > 0)
                domain_id += domain_id_suffix;

            setup.ApplicationName = domain_id;
            setup.DynamicBase = dynamic_dir;
            setup.CachePath = dynamic_dir;

            string dynamic_base = setup.DynamicBase;
            if (CreateDirectory(dynamic_base) && (Environment.GetEnvironmentVariable("MONO_ASPNET_NODELETE") == null))
                ClearDynamicBaseDirectory(dynamic_base);

            #endregion

            //
            // Create app domain
            //
            // _AppDomain appdomain;
            var domain = AppDomain.CurrentDomain;
            bool isDebug = Debugger.IsAttached;
            _AppDomain appdomain = DebugDomain.CreateDomain(domain_id, evidence, setup);
            // string domain_id, object evidence, object setup);
            if (isDebug)
            {
                domain.SetData("DebugDomain", appdomain);
            }
            else
            {
                appdomain = AppDomain.CreateDomain(domain_id, evidence, setup);
                domain.SetData("DebugDomain", appdomain);
            }

            // Populate with the AppDomain data keys expected, Mono only uses a
            // few, but third party apps might use others:
            appdomain.SetData(".appDomain", "*");
            int l = physicalDir.Length;
            if (physicalDir[l - 1] != Path.DirectorySeparatorChar)
                physicalDir += Path.DirectorySeparatorChar;

            appdomain.SetData(".appPath", physicalDir);
            appdomain.SetData(".appVPath", virtualDir);
            appdomain.SetData(".appId", domain_id);
            appdomain.SetData(".domainId", domain_id);
            appdomain.SetData(".hostingVirtualPath", virtualDir);
            appdomain.SetData(".hostingInstallDir", Path.GetDirectoryName(typeof(Object).Assembly.CodeBase));
            appdomain.SetData("DataDirectory", Path.Combine(physicalDir, "App_Data"));
            appdomain.SetData(MonoHostedDataKey, "yes");

            try
            {
                appdomain.DoCallBack(SetHostingEnvironment);
            }
            catch (Exception err) { appdomain.SetData("load.Error", err); }

            if ((appdomain as AppDomain) != null)
                return (appdomain as AppDomain).CreateInstanceAndUnwrap(hostType.Module.Assembly.FullName, hostType.FullName);

            return // (appdomain as DebugDomain)
                AppDomain.CurrentDomain
                .CreateInstanceAndUnwrap(hostType.Module.Assembly.FullName, hostType.FullName);
        }

        static void SetHostingEnvironment()
        {
            bool shadow_copy_enabled = true;
            HostingEnvironmentSection he = WebConfigurationManager.GetWebApplicationSection("system.web/hostingEnvironment") as HostingEnvironmentSection;
            if (he != null)
                shadow_copy_enabled = he.ShadowCopyBinAssemblies;

            if (shadow_copy_enabled)
            {
                AppDomain current = AppDomain.CurrentDomain;
                //current.SetShadowCopyFiles();
                //current.SetShadowCopyPath(current.SetupInformation.PrivateBinPath);
            }

            //HostingEnvironment.IsHosted = true;
            //HostingEnvironment.SiteName = HostingEnvironment.ApplicationID;
        }
    }

}

namespace System
{
    using System.Reflection;
    using System.Runtime.Remoting.Contexts;
    using System.Runtime.Remoting;
    using System.Runtime.Versioning;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Collections.Generic;
    using System.Security.Policy;
    using System.Security.Principal;
    using System.Runtime.CompilerServices;
    using System.Reflection.Emit;

    internal struct AppDomainHandle
    {
        private IntPtr m_appDomainHandle;
        internal AppDomainHandle(IntPtr domainHandle)
        {
            m_appDomainHandle = domainHandle;
        }
    }

    public class _Activator : MarshalByRefObject // , IActivator
    {
        /* [System.Runtime.InteropServices.ComVisible(true)]
        public IConstructionReturnMessage Activate(IConstructionCallMessage msg)
        {
            return RemotingServices.DoCrossContextActivation(msg);
        } */

        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public void LoadAssembly(AssemblyName an)
        {
            /* StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            Assembly a = Assembly.InternalLoad(an, false, null, ref stackMark);
            if (a == null)
                throw new RemotingException(
                    String.Format( "Remoting_AssemblyLoadFailed",
                        an));     
                        */
        }
    }

    public class DebugDomain : MarshalByRefObject, _AppDomain
    {
        public _AppDomain wrap;
        public string ID { get; set; }
        public object Setup { get; set; }
        public Assembly[] AsmList;
        public Evidence Evidence { get; protected set; }

        public DebugDomain()
        {
            Data = new Dictionary<string, object>();
            AsmList = new Assembly[] { };
        }

        // .CreateDomain _AppDomain
        public static DebugDomain CreateDomain(string domain_id, Evidence evidence, object setup)
        {
            var d = new DebugDomain() { ID = domain_id, Evidence = evidence, Setup = setup };
            return d;
        }

        internal static AppDomainManager CurrentAppDomainManager {
            [System.Security.SecurityCritical]  // auto-generated
            get {
                return AppDomain.CurrentDomain.DomainManager;
            }
        }


        #region Get type

        private IntPtr _pDomain = IntPtr.Zero;
        internal AppDomainHandle GetNativeHandle()
        {
            // This should never happen under normal circumstances. However, there ar ways to create an
            // uninitialized object through remoting, etc.
            if (_pDomain == IntPtr.Zero) // .IsNull())
            {
                throw new InvalidOperationException(("Argument_InvalidHandle"));
            }
            return new AppDomainHandle(_pDomain);
        }

        [SecuritySafeCritical]
        internal void GetAppDomainManagerType(out string assembly, out string type)
        {
            // We can't just use our parameters because we need to ensure that the strings used for hte QCall
            // are on the stack.
            string localAssembly = null;
            string localType = null;
            /*
            GetAppDomainManagerType(GetNativeHandle(),
                                    JitHelpers.GetStringHandleOnStack(ref localAssembly),
                                    JitHelpers.GetStringHandleOnStack(ref localType));
 
            assembly = localAssembly;
            type = localType;
            */
            assembly = null; type = null;
        }

        public void GetTypeInfoCount(out uint pcTInfo)
            => (AppDomain.CurrentDomain as _AppDomain).GetTypeInfoCount(out pcTInfo);
        // { pcTInfo = 0; }

        public void GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo)
        {
            (AppDomain.CurrentDomain as _AppDomain).GetTypeInfo(iTInfo, lcid, ppTInfo);
        }

        // [In] 
        public void GetIDsOfNames(ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId)
        {
            (AppDomain.CurrentDomain as _AppDomain).GetIDsOfNames(ref riid, rgszNames, cNames, lcid, rgDispId);
        }

        public void Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags,
            IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr)
        {
            (AppDomain.CurrentDomain as _AppDomain).Invoke(dispIdMember, ref riid, lcid, wFlags, pDispParams,
                 pVarResult, pExcepInfo, puArgErr);
        }

        #endregion

        public override string ToString() => ID;
        public override bool Equals(Object other) => ID.Equals((other as DebugDomain)?.ID ?? "-");
        public override int GetHashCode() => ID.GetHashCode();

        public static Context DefaultContext {
            [System.Security.SecurityCritical]  // auto-generated_required
            get {
                return null; // Thread.GetDomain().GetDefaultContext();
            }
        }

        #region Object, Lifetime

        [System.Security.SecurityCritical]  // auto-generated_required
        public new virtual object GetLifetimeService()
        {
            return null; // LifetimeServices.GetLease(this); 
        }

        // This method is used return lifetime service object. This method
        // can be overridden to return a LifetimeService object with properties unique to
        // this object.
        // For the default Lifetime service this will be an object of type ILease.
        // 
        [System.Security.SecurityCritical]  // auto-generated_required
        public override object InitializeLifetimeService()
        {
            Debugger.Break();
            return null; // LifetimeServices.GetLeaseInitial(this);
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public new virtual ObjRef CreateObjRef(Type requestedType)
        {
            // if(__identity == null)
            //     throw new RemotingException(("Remoting_NoIdentityEntry"));
            Debugger.Break();

            return new ObjRef(this, requestedType);
        }

        public Object CreateInstanceAndUnwrap(String assemblyName,
                                              String typeName)
        {
            ObjectHandle oh = CreateInstance(assemblyName, typeName);
            if (oh == null)
                return null;
            return oh.Unwrap();
        } // CreateInstanceAndUnwrap

        public Object CreateInstanceAndUnwrap(String assemblyName,
                                              String typeName,
                                              Object[] activationAttributes)
        {
            ObjectHandle oh = CreateInstance(assemblyName, typeName, activationAttributes);
            if (oh == null)
                return null;

            return oh.Unwrap();
        } // CreateInstanceAndUnwrap

        public ObjectHandle CreateInstance(String assemblyName, String typeName)
        {
            Debugger.Break();
            return AppDomain.CurrentDomain.CreateInstance(assemblyName, typeName);
        }

        public ObjectHandle CreateInstanceFrom(String assemblyFile, String typeName)
        {
            Debugger.Break();
            return AppDomain.CurrentDomain.CreateInstanceFrom(assemblyFile, typeName, null);
        }

        public ObjectHandle CreateInstance(String assemblyName,
                                    String typeName,
                                    Object[] activationAttributes)
        {
            Debugger.Break();
            return AppDomain.CurrentDomain.CreateInstanceFrom(assemblyName, typeName, activationAttributes);
        }


        public ObjectHandle CreateInstanceFrom(String assemblyFile,
                                        String typeName,
                                        Object[] activationAttributes)
        {
            Debugger.Break();
            return AppDomain.CurrentDomain.CreateInstanceFrom(assemblyFile, typeName, activationAttributes);
        }

        #endregion

        [Obsolete("no Evidence use")]
        public ObjectHandle CreateInstance(String assemblyName,
                                    String typeName,
                                    bool ignoreCase,
                                    BindingFlags bindingAttr,
                                    Binder binder,
                                    Object[] args,
                                     CultureInfo culture,
                                    Object[] activationAttributes,
                                    Evidence securityAttributes)

        {
            Debugger.Break();
            return AppDomain.CurrentDomain.CreateInstance(assemblyName, typeName, ignoreCase, bindingAttr,
                   binder, args, culture, activationAttributes, securityAttributes);
        }

        #pragma warning disable 67

        #region Events

        public event EventHandler DomainUnload;

        [method: System.Security.SecurityCritical]
        public event AssemblyLoadEventHandler AssemblyLoad;

        public event EventHandler ProcessExit;

        [method: System.Security.SecurityCritical]
        public event ResolveEventHandler TypeResolve;

        [method: System.Security.SecurityCritical]
        public event ResolveEventHandler ResourceResolve;

        [method: System.Security.SecurityCritical]
        public event ResolveEventHandler AssemblyResolve;

        [method: System.Security.SecurityCritical]
        public event UnhandledExceptionEventHandler UnhandledException;

        #endregion

        #region Assembly loader 

        [System.Security.SecurityCritical]  // auto-generated
        // [ResourceExposure(ResourceScope.Machine)]
        // [ResourceConsumption(ResourceScope.Machine)]
        internal static AssemblyBuilder InternalDefineDynamicAssembly(
            AssemblyName name,
            AssemblyBuilderAccess access,
            String dir,
            Evidence evidence,
            PermissionSet requiredPermissions,
            PermissionSet optionalPermissions,
            PermissionSet refusedPermissions,
            ref StackCrawlMark stackMark,
            IEnumerable<CustomAttributeBuilder> unsafeAssemblyAttributes,
            SecurityContextSource securityContextSource)
        {
            // if (evidence != null && !AppDomain.CurrentDomain.IsLegacyCasPolicyEnabled)
            //    throw new NotSupportedException(("NotSupported_RequiresCasPolicyExplicit"));

            lock (typeof(AssemblyBuilderLock))
            {
                // we can only create dynamic assemblies in the current domain
                return default(AssemblyBuilder);
                /* 
                return new AssemblyBuilder(AppDomain.CurrentDomain,
                                           name,
                                           access,
                                           dir,
                                           evidence,
                                           requiredPermissions,
                                           optionalPermissions,
                                           refusedPermissions,
                                           ref stackMark,
                                           unsafeAssemblyAttributes,
                                           securityContextSource); */
            } //lock(typeof(AssemblyBuilderLock))
        }
        private class AssemblyBuilderLock { }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access)
        { return null; }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access,
                                              String dir)
        { return null; }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access,
                                              Evidence evidence)
        { return null; }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access,
                                              PermissionSet requiredPermissions,
                                              PermissionSet optionalPermissions,
                                              PermissionSet refusedPermissions)
        { return null; }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access,
                                              String dir,
                                              Evidence evidence)
        { return null; }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access,
                                              String dir,
                                              PermissionSet requiredPermissions,
                                              PermissionSet optionalPermissions,
                                              PermissionSet refusedPermissions)
        { return null; }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access,
                                              Evidence evidence,
                                              PermissionSet requiredPermissions,
                                              PermissionSet optionalPermissions,
                                              PermissionSet refusedPermissions)
        { return null; }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access,
                                              String dir,
                                              Evidence evidence,
                                              PermissionSet requiredPermissions,
                                              PermissionSet optionalPermissions,
                                              PermissionSet refusedPermissions)
        { return null; }

        public AssemblyBuilder DefineDynamicAssembly(AssemblyName name,
                                              AssemblyBuilderAccess access,
                                              String dir,
                                              Evidence evidence,
                                              PermissionSet requiredPermissions,
                                              PermissionSet optionalPermissions,
                                              PermissionSet refusedPermissions,
                                              bool isSynchronized)
        { return null; }


        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public Assembly Load(AssemblyName assemblyRef)
        {
            //StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return null;
            // RuntimeAssembly.InternalLoadAssemblyName(assemblyRef, null, null, ref stackMark, true /*thrownOnFileNotFound*/, false, false);
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public Assembly Load(string assemblyString)
        {
            //StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return null;
            // return RuntimeAssembly.InternalLoad(assemblyString, null, ref stackMark, false);
        }

        public class RuntimeAssembly
        {
            internal static Assembly InternalLoad(string assemblyString, object a, ref StackCrawlMark stackMark,
                     bool fIntrospection)
            {
                return nLoadFile(assemblyString, default(Evidence));
            }

            [System.Security.SecurityCritical]  // auto-generated
            [ResourceExposure(ResourceScope.Machine)]
            [MethodImplAttribute(MethodImplOptions.InternalCall)]
            static internal extern Assembly nLoadFile(String path, Evidence evidence);

            [System.Security.SecurityCritical]  // auto-generated
            [ResourceExposure(ResourceScope.None)]
            [MethodImplAttribute(MethodImplOptions.InternalCall)]
            static internal extern Assembly nLoadImage(byte[] rawAssembly,
                                                            byte[] rawSymbolStore,
                                                            Evidence evidence,
                                                            ref StackCrawlMark stackMark,
                                                            bool fIntrospection,
                                                            SecurityContextSource securityContextSource);

        }

        /*  CustomQueryInterface
       static Guid IID_IManagedObject = new Guid("{C3FCC19E-A970-11D2-8B5A-00A0C9B7C9C4}");

       [System.Security.SecurityCritical]
       CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref Guid iid, out IntPtr ppv) {
           ppv = IntPtr.Zero;
           if (iid == this._iidSourceItf || iid == typeof(NativeMethods.IDispatch).GUID) {
               ppv = Marshal.GetComInterfaceForObject(this, typeof(NativeMethods.IDispatch), CustomQueryInterfaceMode.Ignore);
               return CustomQueryInterfaceResult.Handled;
           }
           else if (iid == IID_IManagedObject)
           {
               return CustomQueryInterfaceResult.Failed;
           }

           return CustomQueryInterfaceResult.NotHandled;
       } */

        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public Assembly Load(byte[] rawAssembly)
        {
            //             StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return null;
            /*
            return RuntimeAssembly.nLoadImage(rawAssembly,
                                       null, // symbol store
                                       null, // evidence
                                       ref stackMark,
                                       false,
                                       SecurityContextSource.CurrentAssembly);
            */
        }

        // public static Assembly ReflectionOnlyLoadFrom(String assemblyFile)
        internal enum StackCrawlMark
        {
            LookForMe = 0,
            LookForMyCaller = 1,
            LookForMyCallersCaller = 2,
            LookForThread = 3
        }

        public int ExecuteAssembly(string asm, Evidence e)
        {
            return 0;
        }
        public int ExecuteAssembly(string asm, Evidence e, string[] a)
        {
            return 0;
        }


        public ObjectHandle CreateInstanceFrom(String assemblyFile,
                                        String typeName,
                                        bool ignoreCase,
                                        BindingFlags bindingAttr,
                                        Binder binder,
                                         Object[] args,
                                        CultureInfo culture,
                                        Object[] activationAttributes,
                                        Evidence securityAttributes)
        { return null; }

        /* 
        public Assembly Load(AssemblyName assemblyRef)           {return null; }
        public Assembly Load(String assemblyString)          {return null; }
        public Assembly Load(byte[] rawAssembly)          {return null; }
        */

        public Assembly Load(byte[] rawAssembly, byte[] rawSymbolStore)
        { return null; }

        public Assembly Load(byte[] rawAssembly, byte[] rawSymbolStore,
                      Evidence securityEvidence)
        { return null; }

        public Assembly Load(AssemblyName assemblyRef, Evidence assemblySecurity)
        { return null; }

        public Assembly Load(String assemblyString,
                      Evidence assemblySecurity)
        { return null; }

        /* [ResourceExposure(ResourceScope.Machine)]
        public int ExecuteAssembly(String assemblyFile, 
                      Evidence assemblySecurity) => 0;
 
        [ResourceExposure(ResourceScope.Machine)]
        public int ExecuteAssembly(String assemblyFile, 
                            Evidence assemblySecurity, 
                            String[] args) => 0; */

        [ResourceExposure(ResourceScope.Machine)]
        public int ExecuteAssembly(String assemblyFile)
        {
            Debugger.Break();
            return 0;
        }

        #endregion

        public String FriendlyName { get => ID; }

        public String BaseDirectory {
            [ResourceExposure(ResourceScope.Machine)]
            get;
            private set;
        }

        public String RelativeSearchPath {
            get;
            private set;
        }

        public bool ShadowCopyFiles {
            get;
            private set;
        }


        public Assembly[] GetAssemblies() => AsmList;

        [System.Security.SecurityCritical]  // auto-generated_required
        public void AppendPrivatePath(String path) { }

        [System.Security.SecurityCritical]  // auto-generated_required
        public void ClearPrivatePath() { }

        [System.Security.SecurityCritical]  // auto-generated_required
        public void SetShadowCopyPath(String s) { }

        [System.Security.SecurityCritical]  // auto-generated_required
        public void ClearShadowCopyPath() { }

        [System.Security.SecurityCritical]  // auto-generated_required
        public void SetCachePath(String s) { }
        //FEATURE_FUSION

        [System.Security.SecurityCritical]  // auto-generated_required
        public void SetData(String name, Object data)
        {
            if (Data.ContainsKey(name))
                Data[name] = data;
            else Data.Add(name, data);

            AppDomain.CurrentDomain.SetData(name, data);
        }

        // #if FEATURE_CORECLR
        // [System.Security.SecurityCritical] // auto-generated
        // #endif
        public Object GetData(string name) => Data[name];

        public IDictionary<string, object> Data { get; set; }

        [System.Security.SecurityCritical]  // auto-generated_required
        public void SetAppDomainPolicy(PolicyLevel domainPolicy) { }

        public void SetThreadPrincipal(IPrincipal principal) { }
        public void SetPrincipalPolicy(PrincipalPolicy policy) { }

        internal const int CTX_FROZEN = 0x00000002;

        // /#mscorlib/system/runtime/remoting/context.cs,db99ae9744885227
        public void DoCallBack(CrossAppDomainDelegate deleg)
        {
            if (deleg == null)
                throw new ArgumentNullException("deleg");

            // if ((_ctxFlags & CTX_FROZEN) == 0)
            //  throw new RemotingException(   (    "Remoting_Contexts_ContextNotFrozenForCallBack"));
            /* Context currCtx = Thread.CurrentContext;
            if (currCtx == this) */
            // We are already in the target context, just execute deleg 
            deleg();

            /* 
            else {
                // We pass 0 for target domain ID for x-context case.
                DoCallBackGeneric(currCtx, this.InternalContextID, deleg);
                // GC.KeepAlive(this);
            }            */
        }

        [System.Security.SecurityCritical]  // auto-generated
        internal void DoCallBackGeneric(Context currCtx,
            IntPtr targetCtxID, CrossContextDelegate deleg)
        {
            /* TransitionCall msgCall = new TransitionCall(targetCtxID, deleg);           
            Message.PropagateCallContextFromThreadToMessage(msgCall);

            IMessage retMsg = this.GetClientContextChain().SyncProcessMessage(msgCall); 
            if (null != retMsg)
                Message.PropagateCallContextFromMessageToThread(retMsg);
            
            IMethodReturnMessage msg = retMsg as IMethodReturnMessage;
            if (null != msg && msg.Exception != null)
                    throw msg.Exception; 
                    */
        }

        public String DynamicDirectory { get; protected set; }
    }

}

