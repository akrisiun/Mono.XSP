//
// Z System.web internal MS IIS classes wrap test 
//
// Authors:
//	MS 

using System;
using System.Web.Hosting;
using System.IO;
using System.Runtime.Remoting;
using System.Diagnostics;
using System.Collections;
using System.Web;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Configuration;
using System.Security;
using System.Security.Policy;
using System.Web.Configuration;

namespace Mono.WebServer
{
    public static class WebCreateHost
    {
        public static IApplicationHost Create(Type type, string virtualDir, string realPath)
        {
            //AppHost = CreateHost.Create(webSource.GetApplicationHostType(), v, realPath) as IApplicationHost;
            // ApplicationHost.CreateApplicationHost(
            // webSource.GetApplicationHostType(), v, realPath) as IApplicationHost;
            IApplicationHost host = null;
            String dir = AppDomain.CurrentDomain.BaseDirectory;

            try {
                Assembly.LoadFile(dir + @"Mono.Posix.dll");
                Assembly.LoadFile(dir + @"Mono.Security.dll");
                Assembly.LoadFile(dir + @"Mono.WebServer.dll");

                host = CreateApplicationHostWrap(type, virtualDir, realPath) as IApplicationHost;
                if (host == null)
                    host = ApplicationHost.CreateApplicationHost(type, virtualDir, realPath) as IApplicationHost;
            }
            catch (Exception ex) {

                //Could not load file or assembly 'Mono.WebServer.XSP, Version=4.4.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756' 
                //or one of its
                //             at System.RuntimeTypeHandle.GetTypeByName(String name, Boolean throwOnError, Boolean ignoreCase, Boolean reflectionOnly, StackCrawlMarkHandle stackMark, IntPtr pPrivHostBinder, Boolean loadTypeFromPartialName, ObjectHandleOnStack type)
                //at System.RuntimeTypeHandle.GetTypeByName(String name, Boolean throwOnError, Boolean ignoreCase, Boolean reflectionOnly, StackCrawlMark & stackMark, IntPtr pPrivHostBinder, Boolean loadTypeFromPartialName)
                //at System.RuntimeType.GetType(String typeName, Boolean throwOnError, Boolean ignoreCase, Boolean reflectionOnly, StackCrawlMark & stackMark)
                //at System.Type.GetType(String typeName, Boolean throwOnError)
                //at System.Web.Hosting.HostingEnvironment.CreateInstance(String assemblyQualifiedName)
                //at System.Web.Hosting.ApplicationHost.CreateApplicationHost(Type hostType, String virtualDir, String physicalDir)

                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            return host;
        }


        // </devdoc>
        //[SecurityPermission(SecurityAction.Demand, Unrestricted = true)]
        //https://referencesource.microsoft.com/?#System.Web/Hosting/ApplicationHost.cs,0b6df2c381e44b38
        public static Object CreateApplicationHostWrap(Type hostType, String virtualDir, String physicalDir)
        {

#if !FEATURE_PAL // FEATURE_PAL does not require PlatformID.Win32NT
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("RequiresNT"); //  SR.GetString(SR.RequiresNT));
#endif // !FEATURE_PAL

            // if (!StringUtil.StringEndsWith(physicalDir, Path.DirectorySeparatorChar))
            if (!physicalDir.EndsWith(Path.DirectorySeparatorChar + ""))
                physicalDir = physicalDir + Path.DirectorySeparatorChar;

            ApplicationManager appManager = ApplicationManager.GetApplicationManager();

            String appId = GetNonRandomizedHashCode(String.Concat(virtualDir, physicalDir)).ToString("x");

            VirtualPath.CheckVirtual();
            ObjectHandle h = // appManager.
                CreateInstanceInNewWorkerAppDomain(
                                hostType, appId, CreateNonRelative(virtualDir), physicalDir);

            return h.Unwrap();
        }

        public static VirtualPath CreateNonRelative(string virtualPath)
        {
            VirtualPath path = null;
            try {
                path = VirtualPath.Create(virtualPath, VirtualPathOptions.AllowAbsolutePath | VirtualPathOptions.AllowAppRelativePath);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            return path;
        }

        public static HostingEnvironment envInstance { get; set; }

        internal static ObjectHandle CreateInstanceInNewWorkerAppDomain(
                                Type type,
                                String appId,
                                VirtualPath virtualPath,
                                String physicalPath)
        {

            //Debug.Trace("AppManager", "CreateObjectInNewWorkerAppDomain, type=" + type.FullName);

            IApplicationHost appHost = null;
            appHost = new SimpleApplicationHost(virtualPath, physicalPath, "Web1", appId ?? "site1");

            //HostingEnvironmentParameters hostingParameters = new HostingEnvironmentParameters();
            //hostingParameters.HostingFlags = HostingEnvironmentFlags.HideFromAppManager;

            HostingEnvironment env = envInstance;
            env = CreateAppDomainWithHostingEnvironmentAndReportErrors(
                        appId, appHost, virtualPath.AppRelativeVirtualPathString); //, hostingParameters);

            var hosted = HostingEnvironment.IsHosted;

            // var objRef = env.CreateObjRef(type);

            //public static IApplicationHost ApplicationHost { get; }
            //public static string ApplicationID { get; }
            //public static string ApplicationPhysicalPath { get; }
            //public static string ApplicationVirtualPath { get; }

            // When marshaling Type, the AppDomain must have FileIoPermission to the assembly, which is not
            // always the case, so we marshal the assembly qualified name instead
            ObjectHandle handle = default(ObjectHandle);
            try {
                // handle = env.CreateInstance(type.AssemblyQualifiedName);

                var assemblyQualifiedName = type.AssemblyQualifiedName;
                Type type2 = Type.GetType(assemblyQualifiedName, true);
                handle = new ObjectHandle(Activator.CreateInstance(type2));

            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }

            return handle;
        }


        private static HostingEnvironment CreateAppDomainWithHostingEnvironmentAndReportErrors(
                    String appId,
                    Mono.WebServer.IApplicationHost appHost,
                    string path)

        // HostingEnvironmentParameters hostingParameters)
        {
            //try {
            // return CreateAppDomainWithHostingEnvironment(appId, appHost, hostingParameters);
            //String physicalPath = appHost.GetPhysicalPath();
            //if (!StringUtil.StringEndsWith(physicalPath, Path.DirectorySeparatorChar))
            //    physicalPath = physicalPath + Path.DirectorySeparatorChar;
            //String domainId = ConstructAppDomainId(appId);
            //String appName = (StringUtil.GetStringHashCode(String.Concat(appId.ToLower(CultureInfo.InvariantCulture),
            //    physicalPath.ToLower(CultureInfo.InvariantCulture)))).ToString("x", CultureInfo.InvariantCulture);

            VirtualPath virtualPath = VirtualPath.Create(path ?? "/", VirtualPathOptions.AllowAllPath); //  appHost.GetVirtualPath());

            //Debug.Trace("AppManager", "CreateAppDomainWithHostingEnvironment, path=" + physicalPath + "; appId=" + appId + "; domainId=" + domainId);

            IDictionary bindings = new Hashtable(20);
            AppDomainSetup setup = new AppDomainSetup();
            //AppDomainSwitches switches = new AppDomainSwitches();
            //PopulateDomainBindings(domainId, appId, appName, physicalPath, virtualPath, setup, bindings);

            // Create the app domain

            AppDomain appDomain = null;
            Dictionary<string, object> appDomainAdditionalData = new Dictionary<string, object>();
            // Exception appDomainCreationException = null;

            //string siteID = appHost.GetSiteID();
            string appSegment = virtualPath.VirtualPathStringNoTrailingSlash;
            bool inClientBuildManager = false;
            Configuration appConfig = null;
            PolicyLevel policyLevel = null;
            PermissionSet permissionSet = null;

            //List<StrongName> fullTrustAssemblies = new List<StrongName>();
            // Set the AppDomainManager if needed
            //Type appDomainManagerType = AspNetAppDomainManager.GetAspNetAppDomainManagerType(requireHostExecutionContextManager, requireHostSecurityManager);
            //if (appDomainManagerType != null) {
            //    setup.AppDomainManagerType = appDomainManagerType.FullName;
            //    setup.AppDomainManagerAssembly = appDomainManagerType.Assembly.FullName;
            //}


            // Create hosting environment in the new app domain
            Type hostType = typeof(HostingEnvironment);
            String module = hostType.Module.Assembly.FullName;
            String typeName = hostType.FullName;
            ObjectHandle h = null;

            // impersonate UNC identity, if any
            IntPtr uncToken = IntPtr.Zero;
            //ImpersonationContext ictx = null;
            //if (uncToken != IntPtr.Zero) {
            //    try {
            //        ictx = new ImpersonationContext(uncToken);
            //    }
            //    catch {
            //    }
            //    finally {
            //        UnsafeNativeMethods.CloseHandle(uncToken);
            //    }
            //}

            try {

                // Create the hosting environment in the app domain
                //#if DBG
                try {
                    h = Activator.CreateInstance(appDomain, module, typeName);
                }
                catch (Exception e) {
                    // Debug.Trace(
                    var user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    Debugger.Log(0, "AppManager",
                        string.Format("appDomain.CreateInstance failed; identity={0}, msg={1}", user, e.Message));
                    // throw;
                }
                //#else
                //  h = Activator.CreateInstance(appDomain, module, typeName);
                //#endif
            }
            finally {
                // revert impersonation
                //if (ictx != null)
                //    ictx.Undo();
                if (h == null && appDomain != null) {
                    AppDomain.Unload(appDomain);
                }
            }

            HostingEnvironment env2 = (h != null) ? h.Unwrap() as HostingEnvironment :
                envInstance ?? new HostingEnvironment();

            if (envInstance == null) {
                envInstance = env2;
            }

            if (env2 == null)
                throw new SystemException("Cannot_create_HostEnv");


            // initialize the hosting environment
            //IConfigMapPathFactory configMapPathFactory = appHost.GetConfigMapPathFactory();
            //if (appDomainStartupConfigurationException == null) {
            //    env.Initialize(this, appHost, configMapPathFactory, hostingParameters, policyLevel);
            //} else {
            //    env.Initialize(this, appHost, configMapPathFactory, hostingParameters, policyLevel, appDomainStartupConfigurationException);
            //}

            return env2;
        }


        internal static int GetNonRandomizedHashCode(string s, bool ignoreCase = false)
        {
            // Preserve the default behavior when string hash randomization is off
            //if (!AppSettings.UseRandomizedStringHashAlgorithm) {
            //    return ignoreCase ? StringComparer.InvariantCultureIgnoreCase.GetHashCode(s) : s.GetHashCode();
            //}

            if (ignoreCase) {
                s = s.ToLower(CultureInfo.InvariantCulture);
            }

            // Use our stable hash algorithm implementation
            return GetStringHashCode(s);
        }

        internal static int GetStringHashCode(string s)
        {
            unsafe
            {
                fixed (char* src = s) {
                    int hash1 = (5381 << 16) + 5381;
                    int hash2 = hash1;

                    // 32bit machines.
                    int* pint = (int*)src;
                    int len = s.Length;
                    while (len > 0) {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                        if (len <= 2) {
                            break;
                        }
                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                        pint += 2;
                        len -= 4;
                    }
                    return hash1 + (hash2 * 1566083941);
                }
            }
        }

    }


    [Serializable]
    //internal
    public
            sealed class VirtualPath : IComparable
    {
        private string _appRelativeVirtualPath;
        private string _virtualPath;

        // const masks into the BitVector32
        private const int isWithinAppRootComputed = 0x00000001;
        private const int isWithinAppRoot = 0x00000002;
        private const int appRelativeAttempted = 0x00000004;

#pragma warning disable 0649
        private SimpleBitVector32 flags;
#pragma warning restore 0649

        //#if DBG
        private static char[] s_illegalVirtualPathChars = new char[] { '\0' };

        // Debug only method to check that the object is in a consistent state
        private void ValidateState()
        {

            Debug.Assert(_virtualPath != null || _appRelativeVirtualPath != null);

            if (_virtualPath != null) {
                CheckValidVirtualPath(_virtualPath);
            }

            if (_appRelativeVirtualPath != null) {
                //Debug.Assert(UrlPath.IsAppRelativePath(_appRelativeVirtualPath));
                CheckValidVirtualPath(_appRelativeVirtualPath);
            }
        }

        private static void CheckValidVirtualPath(string virtualPath)
        {
            Debug.Assert(virtualPath.IndexOfAny(s_illegalVirtualPathChars) < 0);
            Debug.Assert(virtualPath.IndexOf('\\') < 0);
        }
        //#endif

        internal static VirtualPath RootVirtualPath = null;

        private VirtualPath() { }

        public static void CheckVirtual()
        {

            if (RootVirtualPath == null)    // recusion
                RootVirtualPath = new VirtualPath("/"); // VirtualPath.Create("/", VirtualPathOptions.AllowRelativePath);
        }

        static VirtualPath()
        {
            //if (Debugger.IsAttached)
            //    Debugger.Break();

            try {
                RootVirtualPath = VirtualPath.Create("/", VirtualPathOptions.AllowRelativePath | VirtualPathOptions.AllowAbsolutePath);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        #region ctor, methods 1

        // This is called to set the appropriate virtual path field when we already know
        // that the path is generally well formed.
        private VirtualPath(string virtualPath)
        {
            //if (UrlPath.IsAppRelativePath(virtualPath)) {
            //    _appRelativeVirtualPath = virtualPath;
            //} else {
            _virtualPath = virtualPath;
            //}
        }

        int IComparable.CompareTo(object obj)
        {

            VirtualPath virtualPath = obj as VirtualPath;

            // Make sure we're compared to another VirtualPath
            if (virtualPath == null)
                throw new ArgumentException();

            // Check if it's the same object
            if (virtualPath == this)
                return 0;

            return StringComparer.InvariantCultureIgnoreCase.Compare(
                this.VirtualPathString, virtualPath.VirtualPathString);
        }

        public string VirtualPathString {
            get {
                if (_virtualPath == null) {
                    Debug.Assert(_appRelativeVirtualPath != null);

                    // This is not valid if we don't know the app path
                    //if (HttpRuntime.AppDomainAppVirtualPathObject == null) {
                    //    throw new HttpException("VirtualPath_CantMakeAppAbsolute {0}",
                    //        _appRelativeVirtualPath));
                    //}

                    if (_appRelativeVirtualPath.Length == 1) {
                        _virtualPath = HttpRuntime.AppDomainAppVirtualPath ?? Environment.CurrentDirectory;
                    } else {
                        _virtualPath = // HttpRuntime.AppDomainAppVirtualPathString +
                            _appRelativeVirtualPath.Substring(2);
                    }
                }

                return _virtualPath;
            }
        }

        //internal string VirtualPathStringNoTrailingSlash {
        //    get {
        //        return UrlPath.RemoveSlashFromPathIfNeeded(VirtualPathString);
        //    }
        //}

        // Return the virtual path string if we have it, otherwise null
        internal string VirtualPathStringIfAvailable {
            get {
                return _virtualPath;
            }
        }

        internal string AppRelativeVirtualPathStringOrNull {
            get {
                if (_appRelativeVirtualPath == null) {
                    Debug.Assert(_virtualPath != null);

                    // If we already tried to get it and couldn't, return null
                    if (flags[appRelativeAttempted])
                        return null;

                    // This is not valid if we don't know the app path
                    //if (HttpRuntime.AppDomainAppVirtualPathObject == null) {
                    //    throw new HttpException("VirtualPath_CantMakeAppRelative {0}", _virtualPath));
                    //}
                    // _appRelativeVirtualPath = UrlPath.MakeVirtualPathAppRelativeOrNull(_virtualPath);

                    // Remember that we've attempted it
                    flags[appRelativeAttempted] = true;

                    // It could be null if it's not under the app root
                    if (_appRelativeVirtualPath == null)
                        return null;
#if DBG
                    ValidateState();
#endif
                }

                return _appRelativeVirtualPath;
            }
        }

        // Return the app relative path if possible. Otherwise, settle for the absolute.
        public string AppRelativeVirtualPathString {
            get {
                string appRelativeVirtualPath = AppRelativeVirtualPathStringOrNull;
                return (appRelativeVirtualPath != null) ? appRelativeVirtualPath : _virtualPath;
            }
        }

        // Return the app relative virtual path string if we have it, otherwise null
        internal string AppRelativeVirtualPathStringIfAvailable {
            get {
                return _appRelativeVirtualPath;
            }
        }
        // Return the virtual string that's either app relative or not, depending on which
        // one we already have internally.  If we have both, we return absolute
        internal string VirtualPathStringWhicheverAvailable {
            get {
                return _virtualPath != null ? _virtualPath : _appRelativeVirtualPath;
            }
        }

        public string Extension {
            get {
                return UrlPath.GetExtension(VirtualPathString);
            }
        }

        public string FileName {
            get {
                return UrlPath.GetFileName(VirtualPathStringNoTrailingSlash);
            }
        }

        internal string VirtualPathStringNoTrailingSlash {
            get {
                return UrlPath.RemoveSlashFromPathIfNeeded(VirtualPathString);
            }
        }


        // If it's relative, combine it with the app root
        public VirtualPath CombineWithAppRoot()
        {
            return null; // HttpRuntime.AppDomainAppVirtualPathObject.Combine(this);
        }

        public VirtualPath Combine(VirtualPath relativePath)
        {
            if (relativePath == null)
                throw new ArgumentNullException("relativePath");

            // If it's not relative, return it unchanged
            //if (!relativePath.IsRelative)
            //    return relativePath;

            // The base of the combine should never be relative
            //FailIfRelativePath();

            // Get either _appRelativeVirtualPath or _virtualPath
            string virtualPath = VirtualPathStringWhicheverAvailable;

            // Combine it with the relative
            virtualPath = UrlPath.Combine(virtualPath, relativePath.VirtualPathString);

            // Set the appropriate virtual path in the new object
            return new VirtualPath(virtualPath);
        }

        // This simple version of combine should only be used when the relative
        // path is known to be relative.  It's more efficient, but doesn't do any
        // sanity checks.
        internal VirtualPath SimpleCombine(string relativePath)
        {
            return SimpleCombine(relativePath, false /*addTrailingSlash*/);
        }

        internal VirtualPath SimpleCombineWithDir(string directoryName)
        {
            return SimpleCombine(directoryName, true /*addTrailingSlash*/);
        }

        private VirtualPath SimpleCombine(string filename, bool addTrailingSlash)
        {

            // The left part should always be a directory
            //Debug.Assert(HasTrailingSlash);

            // The right part should not start or end with a slash
            Debug.Assert(filename[0] != '/' && !UrlPath.HasTrailingSlash(filename));

            // Use either _appRelativeVirtualPath or _virtualPath
            string virtualPath = VirtualPathStringWhicheverAvailable + filename;
            if (addTrailingSlash)
                virtualPath += "/";

            // Set the appropriate virtual path in the new object
            VirtualPath combinedVirtualPath = new VirtualPath(virtualPath);

            // Copy some flags over to avoid having to recalculate them
            // combinedVirtualPath.CopyFlagsFrom(this, isWithinAppRootComputed | isWithinAppRoot | appRelativeAttempted);
            //#if DBG
            combinedVirtualPath.ValidateState();
            //#endif
            return combinedVirtualPath;
        }

        public VirtualPath MakeRelative(VirtualPath toVirtualPath)
        {
            VirtualPath resultVirtualPath = new VirtualPath();

            // Neither path can be relative

            //TODO
            //FailIfRelativePath();
            //toVirtualPath.FailIfRelativePath();

            // Set it directly since we know the slashes are already ok
            resultVirtualPath._virtualPath = UrlPath.MakeRelative(this.VirtualPathString, toVirtualPath.VirtualPathString);
#if DBG
            resultVirtualPath.ValidateState();
#endif
            return resultVirtualPath;
        }

        public string MapPath()
        {
            return null; // HostingEnvironment.MapPath(this);
        }

        #endregion

        //internal string MapPathInternal()
        //{
        //    return HostingEnvironment.MapPathInternal(this);
        //}

        //internal string MapPathInternal(bool permitNull)
        //{
        //    return HostingEnvironment.MapPathInternal(this, permitNull);
        //}

        //internal string MapPathInternal(VirtualPath baseVirtualDir, bool allowCrossAppMapping)
        //{
        //    return HostingEnvironment.MapPathInternal(this, baseVirtualDir, allowCrossAppMapping);
        //}

        ///////////// VirtualPathProvider wrapper methods /////////////

        //public string GetFileHash(IEnumerable virtualPathDependencies)
        //{
        //    return HostingEnvironment.VirtualPathProvider.GetFileHash(
        //        this, virtualPathDependencies);
        //}

        public static VirtualPath Create(string virtualPath, VirtualPathOptions options)
        {

            // Trim it first, so that blank strings (e.g. "  ") get treated as empty
            if (virtualPath != null)
                virtualPath = virtualPath.Trim();

            // If it's empty, check whether we allow it
            if (String.IsNullOrEmpty(virtualPath)) {
                if ((options & VirtualPathOptions.AllowNull) != 0)
                    return null;

                throw new ArgumentNullException("virtualPath");
            }

            // Dev10 767308: optimize for normal paths, and scan once for
            //     i) invalid chars
            //    ii) slashes
            //   iii) '.'

            bool slashes = false;
            bool dot = false;
            int len = virtualPath.Length;
            unsafe
            {
                fixed (char* p = virtualPath) {
                    for (int i = 0; i < len; i++) {
                        switch (p[i]) {
                            // need to fix slashes ?
                            case '/':
                            if (i > 0 && p[i - 1] == '/')
                                slashes = true;
                            break;
                            case '\\':
                            slashes = true;
                            break;
                            // contains "." or ".."
                            case '.':
                            dot = true;
                            break;
                            // invalid chars
                            case '\0':
                            throw new HttpException(String.Format("Invalid_vpath {0}", virtualPath));
                            default:
                            break;
                        }
                    }
                }
            }

            if (slashes) {
                // If we're supposed to fail on malformed path, then throw
                if ((options & VirtualPathOptions.FailIfMalformed) != 0) {
                    throw new HttpException(String.Format("Invalid_vpath {0}", virtualPath));
                }
                // Flip ----lashes, and remove duplicate slashes                
                virtualPath = UrlPath.FixVirtualPathSlashes(virtualPath);
            }

            // Make sure it ends with a trailing slash if requested
            if ((options & VirtualPathOptions.EnsureTrailingSlash) != 0)
                virtualPath = UrlPath.AppendSlashToPathIfNeeded(virtualPath);

            VirtualPath virtualPathObject = new VirtualPath();

            if (UrlPath.IsAppRelativePath(virtualPath)) {

                if (dot)
                    virtualPath = UrlPath.ReduceVirtualPath(virtualPath);

                if (virtualPath[0] == UrlPath.appRelativeCharacter) {
                    if ((options & VirtualPathOptions.AllowAppRelativePath) == 0) {
                        throw new ArgumentException(String.Format("VirtualPath_AllowAppRelativePath {0}", virtualPath));
                    }

                    virtualPathObject._appRelativeVirtualPath = virtualPath;
                } else {
                    // It's possible for the path to become absolute after calling Reduce,
                    // even though it started with "~/".  e.g. if the app is "/app" and the path is
                    // "~/../hello.aspx", it becomes "/hello.aspx", which is absolute

                    if ((options & VirtualPathOptions.AllowAbsolutePath) == 0) {
                        throw new ArgumentException(String.Format("VirtualPath_AllowAbsolutePath {0}", virtualPath));
                    }

                    virtualPathObject._virtualPath = virtualPath;
                }
            } else {
                if (virtualPath[0] != '/') {
                    if ((options & VirtualPathOptions.AllowRelativePath) == 0) {
                        throw new ArgumentException(String.Format("VirtualPath_AllowRelativePath {0}", virtualPath));
                    }

                    // Don't Reduce relative paths, since the Reduce method is broken (e.g. "../foo.aspx" --> "/foo.aspx!")
                    // 
                    virtualPathObject._virtualPath = virtualPath;
                } else {
                    if ((options & VirtualPathOptions.AllowAbsolutePath) == 0) {
                        throw new ArgumentException(String.Format("VirtualPath_AllowAbsolutePath {0}", virtualPath));
                    }

                    if (dot)
                        virtualPath = UrlPath.ReduceVirtualPath(virtualPath);

                    virtualPathObject._virtualPath = virtualPath;
                }
            }
            //#if DBG
            virtualPathObject.ValidateState();
            //#endif
            return virtualPathObject;
        }
    }

    internal static class UrlPath
    {

        internal const char appRelativeCharacter = '~';
        internal const string appRelativeCharacterString = "~/";

        private static char[] s_slashChars = new char[] { '\\', '/' };

        internal static bool IsRooted(String basepath)
        {
            return (String.IsNullOrEmpty(basepath) || basepath[0] == '/' || basepath[0] == '\\');
        }

        // Checks if virtual path contains a protocol, which is referred to as a scheme in the
        // URI spec.
        private static bool HasScheme(string virtualPath)
        {
            // URIs have the format <scheme>:<scheme-specific-path>, e.g. mailto:user@ms.com,
            // http://server/, nettcp://server/, etc.  The <scheme> cannot contain slashes.
            // The virtualPath passed to this method may be absolute or relative. Although
            // ':' is only allowed in the <scheme-specific-path> if it is encoded, the 
            // virtual path that we're receiving here may be decoded, so it is impossible
            // for us to determine if virtualPath has a scheme.  We will be conservative
            // and err on the side of assuming it has a scheme when we cannot tell for certain.
            // To do this, we first check for ':'.  If not found, then it doesn't have a scheme.
            // If ':' is found, then as long as we find a '/' before the ':', it cannot be
            // a scheme because schemes don't contain '/'.  Otherwise, we will assume it has a 
            // scheme.
            int indexOfColon = virtualPath.IndexOf(':');
            if (indexOfColon == -1)
                return false;
            int indexOfSlash = virtualPath.IndexOf('/');
            return (indexOfSlash == -1 || indexOfColon < indexOfSlash);
        }

        // Returns whether the virtual path is relative.  Note that this returns true for
        // app relative paths (e.g. "~/sub/foo.aspx")
        internal static bool IsRelativeUrl(string virtualPath)
        {
            // If it has a protocol, it's not relative
            if (HasScheme(virtualPath))
                return false;

            return !IsRooted(virtualPath);
        }


        internal static bool IsAppRelativePath(string path)
        {

            if (path == null)
                return false;

            int len = path.Length;

            // Empty string case
            if (len == 0) return false;

            // It must start with ~
            if (path[0] != appRelativeCharacter)
                return false;

            // Single character case: "~"
            if (len == 1)
                return true;

            // If it's longer, checks if it starts with "~/" or "~\"
            return path[1] == '\\' || path[1] == '/';
        }

        internal static bool IsValidVirtualPathWithoutProtocol(string path)
        {
            if (path == null)
                return false;
            return !HasScheme(path);
        }

        internal static String GetDirectory(String path)
        {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentException("Empty_path_has_no_directory");

            if (path[0] != '/' && path[0] != appRelativeCharacter)
                throw new ArgumentException(String.Format("Path_must_be_rooted", path));

            // If it's just "~" or "/", return it unchanged
            if (path.Length == 1)
                return path;

            int slashIndex = path.LastIndexOf('/');

            // This could happen if the input looks like "~abc"
            if (slashIndex < 0)
                throw new ArgumentException(String.Format("Path_must_be_rooted {0}", path));

            return path.Substring(0, slashIndex + 1);
        }

        private static bool IsDirectorySeparatorChar(char ch)
        {
            return (ch == '\\' || ch == '/');
        }

        internal static bool IsAbsolutePhysicalPath(string path)
        {
            if (path == null || path.Length < 3)
                return false;

            // e.g c:\foo
            if (path[1] == ':' && IsDirectorySeparatorChar(path[2]))
                return true;

            // e.g \\server\share\foo or //server/share/foo
            return IsUncSharePath(path);
        }

        internal static bool IsUncSharePath(string path)
        {
            // e.g \\server\share\foo or //server/share/foo
            if (path.Length > 2 && IsDirectorySeparatorChar(path[0]) && IsDirectorySeparatorChar(path[1]))
                return true;
            return false;

        }

        internal static void CheckValidVirtualPath(string path)
        {

            // Check if it looks like a physical path (UNC shares and C:)
            if (IsAbsolutePhysicalPath(path)) {
                throw new HttpException(String.Format("Physical_path_not_allowed {0}", path));
            }

            // Virtual path can't have colons.
            int iqs = path.IndexOf('?');
            if (iqs >= 0) {
                path = path.Substring(0, iqs);
            }
            if (HasScheme(path)) {
                throw new HttpException(String.Format("Invalid_vpath {0}", path));
            }
        }

        private static String Combine(String appPath, String basepath, String relative)
        {
            String path;

            if (String.IsNullOrEmpty(relative))
                throw new ArgumentNullException("relative");
            if (String.IsNullOrEmpty(basepath))
                throw new ArgumentNullException("basepath");

            if (basepath[0] == appRelativeCharacter && basepath.Length == 1) {
                // If it's "~", change it to "~/"
                basepath = appRelativeCharacterString;
            } else {
                // If the base path includes a file name, get rid of it before combining
                int lastSlashIndex = basepath.LastIndexOf('/');
                Debug.Assert(lastSlashIndex >= 0);
                if (lastSlashIndex < basepath.Length - 1) {
                    basepath = basepath.Substring(0, lastSlashIndex + 1);
                }
            }

            // Make sure it's a virtual path (ASURT 73641)
            CheckValidVirtualPath(relative);

            if (IsRooted(relative)) {
                path = relative;
            } else {

                // If the path is exactly "~", just return the app root path
                if (relative.Length == 1 && relative[0] == appRelativeCharacter)
                    return appPath;

                // If the relative path starts with "~/" or "~\", treat it as app root
                // relative (ASURT 68628)
                if (IsAppRelativePath(relative)) {
                    if (appPath.Length > 1)
                        path = appPath + "/" + relative.Substring(2);
                    else
                        path = "/" + relative.Substring(2);
                } else {
                    path = SimpleCombine(basepath, relative);
                }
            }

            return Reduce(path);
        }

        internal static String Combine(String basepath, String relative)
        {
            return Combine(AppDomainAppVirtualPathString, basepath, relative);
        }

        internal static String AppDomainAppVirtualPathString {
            get {

                string _appDomainAppVPath = null;
                // _appDomainAppVPath = VirtualPath.CreateNonRelativeTrailingSlash(GetAppDomainString(".appVPath"));
                _appDomainAppVPath = (GetAppDomainString(BaseApplicationHost.appVPath));

                return _appDomainAppVPath;
                //return VirtualPath.GetVirtualPathString(_appDomainAppVPath);
            }
        }


        private static String GetAppDomainString(String key)
        {
            Object x = Thread.GetDomain().GetData(key);

            return x as String;
        }

        internal static void AddAppDomainTraceMessage(String message)
        {
            const String appDomainTraceKey = "ASP.NET Domain Trace";
            AppDomain d = Thread.GetDomain();
            String m = d.GetData(appDomainTraceKey) as String;
            d.SetData(appDomainTraceKey, (m != null) ? m + " ... " + message : message);
        }


        // This simple version of combine should only be used when the relative
        // path is known to be relative.  It's more efficient, but doesn't do any
        // sanity checks.
        internal static String SimpleCombine(String basepath, String relative)
        {
            Debug.Assert(!String.IsNullOrEmpty(basepath));
            Debug.Assert(!String.IsNullOrEmpty(relative));
            Debug.Assert(relative[0] != '/');

            if (HasTrailingSlash(basepath))
                return basepath + relative;
            else
                return basepath + "/" + relative;
        }

        internal static String Reduce(String path)
        {

            // ignore query string
            String queryString = null;
            if (path != null) {
                int iqs = path.IndexOf('?');
                if (iqs >= 0) {
                    queryString = path.Substring(iqs);
                    path = path.Substring(0, iqs);
                }
            }

            // Take care of backslashes and duplicate slashes
            path = FixVirtualPathSlashes(path);

            path = ReduceVirtualPath(path);

            return (queryString != null) ? (path + queryString) : path;
        }

        // Same as Reduce, but for a virtual path that is known to be well formed
        internal static String ReduceVirtualPath(String path)
        {

            int length = path.Length;
            int examine;

            // quickly rule out situations in which there are no . or ..

            for (examine = 0; ; examine++) {
                examine = path.IndexOf('.', examine);
                if (examine < 0)
                    return path;

                if ((examine == 0 || path[examine - 1] == '/')
                    && (examine + 1 == length || path[examine + 1] == '/' ||
                        (path[examine + 1] == '.' && (examine + 2 == length || path[examine + 2] == '/'))))
                    break;
            }

            // OK, we found a . or .. so process it:

            ArrayList list = new ArrayList();
            StringBuilder sb = new StringBuilder();
            int start;
            examine = 0;

            for (;;) {
                start = examine;
                examine = path.IndexOf('/', start + 1);

                if (examine < 0)
                    examine = length;

                if (examine - start <= 3 &&
                    (examine < 1 || path[examine - 1] == '.') &&
                    (start + 1 >= length || path[start + 1] == '.')) {
                    if (examine - start == 3) {
                        if (list.Count == 0)
                            throw new HttpException("Cannot_exit_up_top_directory ");

                        // We're about to backtrack onto a starting '~', which would yield
                        // incorrect results.  Instead, make the path App Absolute, and call
                        // Reduce on that.
                        if (list.Count == 1 && IsAppRelativePath(path)) {
                            Debug.Assert(sb.Length == 1);
                            //return ReduceVirtualPath(
                            //    MakeVirtualPathAppAbsolute(path));
                        }

                        sb.Length = (int)list[list.Count - 1];
                        list.RemoveRange(list.Count - 1, 1);
                    }
                } else {
                    list.Add(sb.Length);

                    sb.Append(path, start, examine - start);
                }

                if (examine == length)
                    break;
            }

            string result = sb.ToString();

            // If we end up with en empty string, turn it into either "/" or "." (VSWhidbey 289175)
            if (result.Length == 0) {
                if (length > 0 && path[0] == '/')
                    result = @"/";
                else
                    result = ".";
            }

            return result;
        }

        // Change backslashes to forward slashes, and remove duplicate slashes
        internal static String FixVirtualPathSlashes(string virtualPath)
        {

            // Make sure we don't have any back slashes
            virtualPath = virtualPath.Replace('\\', '/');

            // Replace any double forward slashes
            for (;;) {
                string newPath = virtualPath.Replace("//", "/");

                // If it didn't do anything, we're done
                if ((object)newPath == (object)virtualPath)
                    break;

                // We need to loop again to take care of triple (or more) slashes (VSWhidbey 288782)
                virtualPath = newPath;
            }

            return virtualPath;
        }

        // We use file: protocol instead of http:, so that Uri.MakeRelative behaves
        // in a case insensitive way (VSWhidbey 80078)
        private const string dummyProtocolAndServer = "file://foo";

        // Return the relative vpath path from one rooted vpath to another
        internal static string MakeRelative(string from, string to)
        {

            // If either path is app relative (~/...), make it absolute, since the Uri
            // class wouldn't know how to deal with it.

            // TODO
            //from = MakeVirtualPathAppAbsolute(from);
            //to = MakeVirtualPathAppAbsolute(to);

            // Make sure both virtual paths are rooted
            if (!IsRooted(from))
                throw new ArgumentException(String.Format("Path_must_be_rooted {0}", from));
            if (!IsRooted(to))
                throw new ArgumentException(String.Format("Path_must_be_rooted {0}", to));

            // Remove the query string, so that System.Uri doesn't corrupt it
            string queryString = null;
            if (to != null) {
                int iqs = to.IndexOf('?');
                if (iqs >= 0) {
                    queryString = to.Substring(iqs);
                    to = to.Substring(0, iqs);
                }
            }

            // Uri's need full url's so, we use a dummy root
            Uri fromUri = new Uri(dummyProtocolAndServer + from);
            Uri toUri = new Uri(dummyProtocolAndServer + to);

            string relativePath;

            // VSWhidbey 144946: If to and from points to identical path (excluding query and fragment), just use them instead
            // of returning an empty string.
            if (fromUri.Equals(toUri)) {
                int iPos = to.LastIndexOfAny(s_slashChars);

                if (iPos >= 0) {

                    // If it's the same directory, simply return "./"
                    // Browsers should interpret "./" as the original directory.
                    if (iPos == to.Length - 1) {
                        relativePath = "./";
                    } else {
                        relativePath = to.Substring(iPos + 1);
                    }
                } else {
                    relativePath = to;
                }
            } else {
                // To avoid deprecation warning.  It says we should use MakeRelativeUri instead (which returns a Uri),
                // but we wouldn't gain anything from it.  The way we use MakeRelative is hacky anyway (fake protocol, ...),
                // and I don't want to take the chance of breaking something with this change.
#pragma warning disable 0618
                relativePath = fromUri.MakeRelative(toUri);
#pragma warning restore 0618
            }

            // Note that we need to re-append the query string and fragment (e.g. #anchor)
            return relativePath + queryString + toUri.Fragment;
        }

        internal static string GetDirectoryOrRootName(string path)
        {
            string dir;

            dir = Path.GetDirectoryName(path);
            if (dir == null) {
                dir = Path.GetPathRoot(path);
            }

            return dir;
        }

        internal static string GetFileName(string virtualPath)
        {
            // Code copied from CLR\BCL\System\IO\Path.cs
            //    - Check for invalid chars removed
            //    - Only '/' is used as separator (path.cs also used '\' and ':')
            if (virtualPath != null) {
                int length = virtualPath.Length;
                for (int i = length; --i >= 0;) {
                    char ch = virtualPath[i];
                    if (ch == '/')
                        return virtualPath.Substring(i + 1, length - i - 1);

                }
            }
            return virtualPath;
        }

        internal static string GetFileNameWithoutExtension(string virtualPath)
        {
            // Code copied from CLR\BCL\System\IO\Path.cs
            //    - Check for invalid chars removed
            virtualPath = GetFileName(virtualPath);
            if (virtualPath != null) {
                int i;
                if ((i = virtualPath.LastIndexOf('.')) == -1)
                    return virtualPath; // No extension found
                else
                    return virtualPath.Substring(0, i);
            }
            return null;
        }

        internal static string GetExtension(string virtualPath)
        {
            if (virtualPath == null)
                return null;

            int length = virtualPath.Length;
            for (int i = length; --i >= 0;) {
                char ch = virtualPath[i];
                if (ch == '.') {
                    if (i != length - 1)
                        return virtualPath.Substring(i, length - i);
                    else
                        return String.Empty;
                }
                if (ch == '/')
                    break;
            }
            return String.Empty;
        }

        internal static bool HasTrailingSlash(string virtualPath)
        {
            return virtualPath[virtualPath.Length - 1] == '/';
        }

        internal static string AppendSlashToPathIfNeeded(string path)
        {

            if (path == null) return null;

            int l = path.Length;
            if (l == 0) return path;

            if (path[l - 1] != '/')
                path += '/';

            return path;
        }

        //
        // Remove the trailing forward slash ('/') except in the case of the root ("/").
        // If the string is null or empty, return null, which represents a machine.config or root web.config.
        //
        internal static string RemoveSlashFromPathIfNeeded(string path)
        {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            int l = path.Length;
            if (l <= 1 || path[l - 1] != '/') {
                return path;
            }

            return path.Substring(0, l - 1);
        }
    }

    [Flags]
    public // internal 
        enum VirtualPathOptions
    {
        AllowNull = 0x00000001,
        EnsureTrailingSlash = 0x00000002,
        AllowAbsolutePath = 0x00000004,
        AllowAppRelativePath = 0x00000008,
        AllowRelativePath = 0x00000010,
        FailIfMalformed = 0x00000020,

        AllowAllPath = AllowAbsolutePath | AllowAppRelativePath | AllowRelativePath,
    }

    [Serializable]
    internal struct SimpleBitVector32
    {
        private int data;

        internal SimpleBitVector32(int data)
        {
            this.data = data;
        }

        internal int Data {
            get { return data; }
            //#if UNUSED_CODE
            set { data = value; }
            //#endif
        }

        internal bool this[int bit] {
            get {
                return (data & bit) == bit;
            }
            set {
                int _data = data;
                if (value) {
                    data = _data | bit;
                } else {
                    data = _data & ~bit;
                }
            }
        }
    }

}

namespace System.Web.Hosting
{
    using System;
    using System.Collections;
    using System.Configuration;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using System.Web;
    using System.Web.Configuration;
    //using System.Web.Util;
    using Microsoft.Win32;
    using Mono.WebServer;
    using Debug = System.Diagnostics;
    //System.Web.Util.Debug;

    internal class SimpleApplicationHost : MarshalByRefObject,
        System.Web.Hosting.IApplicationHost,
        Mono.WebServer.IApplicationHost
    {
        private VirtualPath _appVirtualPath;
        private String _appPhysicalPath;

        public string AppDomainAppVirtualPath { get; set; }

        internal SimpleApplicationHost(VirtualPath virtualPath, string physicalPath, string siteName, string siteID)
        {
            //if (String.IsNullOrEmpty(physicalPath))
            //    throw ExceptionUtil.ParameterNullOrEmpty("physicalPath");
            // Throw if the physical path is not canonical, to prevent potential
            // security issues (VSWhidbey 418125)
            //if (FileUtil.IsSuspiciousPhysicalPath(physicalPath)) {
            //    throw ExceptionUtil.ParameterInvalid(physicalPath);
            //}

            AppDomainAppVirtualPath = HttpRuntime.AppDomainAppVirtualPath ?? virtualPath.AppRelativeVirtualPathString;

            SiteName = siteName;
            SiteID = siteID;

            _appVirtualPath = virtualPath;
            _appPhysicalPath = StringEndsWith(physicalPath, "\\") ? physicalPath : physicalPath + "\\";
        }

        static bool StringEndsWith(string a, string b)
        {
            return a.EndsWith(b);
        }

        public override Object InitializeLifetimeService()
        {
            return null; // never expire lease
        }

        // IApplicationHost implementation
        public string GetVirtualPath()
        {
            return _appVirtualPath.VirtualPathString;
        }

        String IApplicationHost.GetPhysicalPath()
        {
            return _appPhysicalPath;
        }

        IConfigMapPathFactory IApplicationHost.GetConfigMapPathFactory()
        {
            return new SimpleConfigMapPathFactory();
        }

        IntPtr IApplicationHost.GetConfigToken()
        {
            return IntPtr.Zero;
        }

        public string SiteName { get; set; }
        public string SiteID { get; set; }

        public string Path {
            get {
                throw new NotImplementedException();
            }
        }

        public string VPath {
            get {
                throw new NotImplementedException();
            }
        }

        public AppDomain Domain {
            get {
                return AppDomain.CurrentDomain;
            }
        }

        public IRequestBroker RequestBroker { get; set; }

        public ApplicationServer Server { get; set; }

        String IApplicationHost.GetSiteName()
        {
            return SiteName; //  WebConfigurationHost.DefaultSiteName;
        }

        String IApplicationHost.GetSiteID()
        {
            return SiteID; //  WebConfigurationHost.DefaultSiteID;
        }

        public void MessageReceived()
        {
            // nothing
        }

        public void Unload()
        {

        }

        public bool IsHttpHandler(string verb, string uri)
        {
            throw new NotImplementedException();
        }
    }

    [Serializable()]
    internal class SimpleConfigMapPathFactory : IConfigMapPathFactory
    {
        IConfigMapPath IConfigMapPathFactory.Create(string virtualPath, string physicalPath)
        {
            WebConfigurationFileMap webFileMap = new WebConfigurationFileMap();
            VirtualPath vpath = VirtualPath.Create(virtualPath, VirtualPathOptions.AllowAllPath);

            // Application path
            webFileMap.VirtualDirectories.Add(vpath.VirtualPathStringNoTrailingSlash,
                new VirtualDirectoryMapping(physicalPath, true));

            // Client script file path
            //webFileMap.VirtualDirectories.Add(
            //        HttpRuntime.AspClientScriptVirtualPath,
            //        new VirtualDirectoryMapping(HttpRuntime.AspClientScriptPhysicalPathInternal, false));

            return new UserMapPath(webFileMap);
        }
    }


    public static class CheckWorker
    {
        static CheckWorker()
        {
            //if (Debugger.IsAttached)
            //    Debugger.Break();

            _asyncEndOfSendCallback = new HttpWorkerRequest.EndOfSendNotification(EndOfSendCallback);
            _handlerCompletionCallback = new AsyncCallback(OnHandlerCompletion);
        }

        public static void Request(Mono.WebServer.IApplicationHost appHost)
        {
            System.Web.Hosting.IApplicationHost iisHost = appHost as System.Web.Hosting.IApplicationHost;

            if (Debugger.IsAttached)
                Debugger.Break();

            var domain = Thread.GetDomain();
            domain.SetData(BaseApplicationHost.appPath, null);

            var _appPhysPath = domain.GetData(BaseApplicationHost.appPath) as string;
            if (_appPhysPath != null) // Thread.GetDomain().GetData(".appPath") != null) 
            {
                throw new HttpException("Wrong_SimpleWorkerRequest");
            }

            var _appVirtPath = domain.GetData(BaseApplicationHost.appVPath); //  ToString();
            if (_appVirtPath == null)
                domain.SetData(BaseApplicationHost.appVPath, appHost.VPath ?? "/");

            //  HttpRuntime.AspInstallDirectoryInternal;
        }

        /*
         * 
         *  /// </devdoc>
         *  https://referencesource.microsoft.com/?#System.Web/Hosting/SimpleWorkerRequest.cs,c584f2ce273b03af
            public SimpleWorkerRequest(String page, String query, TextWriter output): this() {
                _queryString = query;
                _output = output;
                _page = page;

                ExtractPagePathInfo();

                _appPhysPath = Thread.GetDomain().GetData(BaseApplicationHost.appPath).ToString();
                _appVirtPath = Thread.GetDomain().GetData(".appVPath").ToString();
                _installDir = HttpRuntime.AspInstallDirectoryInternal;

                _hasRuntimeInfo = true;
            }

             //*  Ctor that gets application data as arguments,assuming HttpRuntime
             //*  has not been set up.
             //*
             //*  This allows for limited functionality to execute handlers.

                /// <devdoc>
                ///    <para>[To be supplied.]</para>
                /// </devdoc>
                public SimpleWorkerRequest(String appVirtualDir, String appPhysicalDir, String page, String query, TextWriter output): this() {
                if (Thread.GetDomain().GetData(".appPath") != null) {
                    throw new HttpException(SR.GetString(SR.Wrong_SimpleWorkerRequest));
                }

                _appVirtPath = appVirtualDir;
                _appPhysPath = appPhysicalDir;
                _queryString = query;
                _output = output;
                _page = page;

                ExtractPagePathInfo();

                if (!StringUtil.StringEndsWith(_appPhysPath, '\\'))
                    _appPhysPath += "\\";

                _hasRuntimeInfo = false;
            }
            */

        public static void ProcessRequest(this MonoWorkerRequest req)
        {
            Exception error = null;
            //var pipeline = (HttpRuntime.UseIntegratedPipeline);
            var vpath = HttpRuntime.AppDomainAppVirtualPath;
            var asp_path = HttpRuntime.AspInstallDirectory;


            try {
                //HttpRuntime.ProcessRequest(req);
                //var apppath = HttpRuntime.AppDomainAppPath;
                //var bin_path = HttpRuntime.BinDirectory;

                ProcessRequestInternal(req);

                if (error == null && HttpContext.Current != null) {
                    var ctx = HttpContext.Current;
                    var list = ctx.AllErrors;
                    if (list != null && list.Length > 0)
                        error = list[0];
                }
            }
            catch (Exception ex) { error = ex; }

            if (error != null)
                throw error;
        }

        private static void ProcessRequestInternal(HttpWorkerRequest wr)
        {

            // Construct the Context on HttpWorkerRequest, hook everything together
            HttpContext context;
            HttpContext.Current = null;


            try {
                context = new HttpContext(wr); // , false /* initResponseWriter */);

                HttpContext.Current = context;
            }
            catch {
                try {
                    // If we fail to create the context for any reason, send back a 400 to make sure
                    // the request is correctly closed (relates to VSUQFE3962)
                    wr.SendStatus(400, "Bad Request");
                    wr.SendKnownResponseHeader(HttpWorkerRequest.HeaderContentType, "text/html; charset=utf-8");

                    byte[] body = Encoding.ASCII.GetBytes("<html><body>Bad Request</body></html>");
                    wr.SendResponseFromMemory(body, body.Length);
                    wr.FlushResponse(true);
                    wr.EndOfRequest();
                    return;
                }
                finally {
                    //Interlocked.Decrement(ref _activeRequestCount);
                }
            }

            //wr.SetEndOfSendNotification(_asyncEndOfSendCallback, context);

            HostingEnvironment.IncrementBusyCount();

            try {
                // First request initialization
                try {
                    //EnsureFirstRequestInit(context);
                }
                catch {
                    // If we are handling a DEBUG request, ignore the FirstRequestInit exception.
                    // This allows the HttpDebugHandler to execute, and lets the debugger attach to
                    // the process (VSWhidbey 358135)

                    //if (!context.Request.IsDebuggingRequest) {
                    //    throw;
                    //}
                }

                // Init response writer (after we have config in first request init)
                // no need for impersonation as it is handled in config system
                //context.Response.InitResponseWriter();

                // Get application instance
                IHttpHandler app = HttpApplicationFactory.GetApplicationInstance(context);

                if (app == null)
                    throw new HttpException("Unable_create_app_object");

                //if (EtwTrace.IsTraceEnabled(EtwTraceLevel.Verbose, EtwTraceFlags.Infrastructure)) 
                //    EtwTrace.Trace(EtwTraceType.ETW_TYPE_START_HANDLER, context.WorkerRequest, app.GetType().FullName, "Start");

                if (app is IHttpAsyncHandler) {
                    // asynchronous handler
                    IHttpAsyncHandler asyncHandler = (IHttpAsyncHandler)app;
                    // context.AsyncAppHandler = asyncHandler;
                    context.Handler = asyncHandler;
                    // asyncHandler.
                    BeginProcessRequest(context, _handlerCompletionCallback, context);

                } else {
                    // synchronous handler
                    app.ProcessRequest(context);
                    //FinishRequest(context.WorkerRequest, context, null);
                }
            }
            catch (Exception e) {

                // context.Response.InitResponseWriter();
                Console.WriteLine(e.Message);
                throw e;
                //FinishRequest(wr, context, e);
            }

            HostingEnvironment.DecrementBusyCount();
        }

        public static IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback callback, Object state)
        {

            // DDB 168193: DefaultHttpHandler is obsolate in integrated mode
            // if (HttpRuntime.UseIntegratedPipeline) {
            var _context = context;
            HttpResponse response = _context.Response;

            //if (response.CanExecuteUrlForEntireResponse) {
            //    // use ExecuteUrl
            //    String path = OverrideExecuteUrlPath();

            //    if (path != null && !HttpRuntime.IsFullTrust) {
            //        // validate that an untrusted derived classes (not in GAC)
            //        // didn't set the path to a place that CAS wouldn't let it access
            //        if (!this.GetType().Assembly.GlobalAssemblyCache) {
            //            HttpRuntime.CheckFilePermission(MapPathWithAssert(context, path));
            //        }
            //    }

            //    return response.BeginExecuteUrlForEntireResponse(path, _executeUrlHeaders, callback, state);
            //} else 
            {

                // let the base class throw if it doesn't want static files handler
                //OnExecuteUrlPreconditionFailure();

                // use static file handler
                _context = null; // don't keep request data alive in sync case

                HttpRequest request = context.Request;

                // refuse POST requests
                if (request.HttpMethod == "POST") { // .HttpVerb == HttpVerb.POST) {
                    throw new HttpException(405, string.Format("SR.Method_not_allowed {0} {1}", request.HttpMethod, request.Path));
                }

                // refuse .asp requests
                //if (IsClassicAspRequest(request.FilePath)) {

                // default to static file handler
                //StaticFileHandler.ProcessRequestInternal(context, OverrideExecuteUrlPath());

                // return async result indicating completion
                return new HttpAsyncResult(callback, state, true, null, null);
            }
        }

        private static void OnHandlerCompletion(IAsyncResult ar)
        {
            HttpContext context = (HttpContext)ar.AsyncState;

            try {
                IHttpAsyncHandler asyncAppHandler = context.Handler as IHttpAsyncHandler;

                if (asyncAppHandler != null)
                    asyncAppHandler.EndProcessRequest(ar);
            }
            catch (Exception e) {
                context.AddError(e);
            }
            finally {
                // no longer keep AsyncAppHandler poiting to the application
                // is only needed to call EndProcessRequest

                // context.AsyncAppHandler = null;
                // context.Handler = null;
            }

            //FinishRequest(context.WorkerRequest, context, context.Error);
        }


        private static void EndOfSendCallback(HttpWorkerRequest wr, Object arg)
        {
            // Debug.Trace("PipelineRuntime", "HttpRuntime.EndOfSendCallback");

            HttpContext context = (HttpContext)arg;
            var req = (IDisposable)((object)context.Request);
            req.Dispose();

            var resp = (IDisposable)((object)context.Response);
            resp.Dispose();
        }

        private static HttpWorkerRequest.EndOfSendNotification _asyncEndOfSendCallback;
        private static AsyncCallback _handlerCompletionCallback;

        public static AsyncCallback HandlerCompletionCallback {
            [DebuggerStepThrough]
            get { return _handlerCompletionCallback; }
        }

        internal static IHttpHandler GetApplicationInstance<T>(HttpContext context) where T : HttpApplication
        {
            if (_customApplication != null)
                return _customApplication;

            // Check to see if it's a debug auto-attach request
            //if (context.Request.IsDebuggingRequest)
            //    return new HttpDebugHandler();

            if (_theApplicationFactory == null)
                return null;

            //_theApplicationFactory.EnsureInited();

            //_theApplicationFactory.EnsureAppStartCalled(context);

            return _theApplicationFactory.GetNormalApplicationInstance(context, typeof(T).GetType());
        }

        static HttpApplicationFactory _theApplicationFactory = null;
        static IHttpHandler _customApplication = null;
    }

    public class HttpApplicationFactory
    {
        internal const string applicationFileName = "global.asax";
        public static Type HttpAppType { get; private set; }
        static HttpApplicationFactory()
        {
            HttpAppType = typeof(HttpApp); //  lication));
        }

        public static void SetType<T>() where T : HttpApp
        {
            HttpAppType = typeof(T);
        }

        // the only instance of application factory
        private static HttpApplicationFactory _theApplicationFactory = new HttpApplicationFactory();

        internal static Object CreateNonPublicInstance(Type type, Object[] args = null)
        {
            return Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.CreateInstance,
                null,
                args,
                null);
        }

        public static HttpApplication GetApplicationInstance(HttpContext context)
        {
            HttpApplication app = null;

            app = _theApplicationFactory.GetNormalApplicationInstance(context, HttpAppType);
            return app;
        }

        public HttpApplication GetNormalApplicationInstance(HttpContext context, Type _theApplicationType = null)
        {
            HttpApplication app = null;

            //lock (_freeList) {
            //    if (_numFreeAppInstances > 0) {
            //        app = (HttpApplication)_freeList.Pop();
            //        _numFreeAppInstances--;


            if (app == null) {
                // If ran out of instances, create a new one
                app = (HttpApplication)
                    // HttpRuntime.
                    CreateNonPublicInstance(_theApplicationType);

                //using (new ApplicationImpersonationContext()) {
                //
                //    app.InitInternal(context, _state, _eventHandlerMethods);
                HttpApp.InitInternal(app, context); //, _state)
                //
                //}
            }

            //if (AppSettings.UseTaskFriendlySynchronizationContext) {
            //    // When this HttpApplication instance is no longer in use, recycle it.
            //    app.ApplicationInstanceConsumersCounter = new CountdownTask(1); // representing required call to HttpApplication.ReleaseAppInstance
            //    app.ApplicationInstanceConsumersCounter.Task.ContinueWith((_, o) => RecycleApplicationInstance((HttpApplication)o), app, TaskContinuationOptions.ExecuteSynchronously);
            //}

            return app;
        }

    }

    // 
    public class AppHttpAsyncResult : HttpAsyncResult
    {
        AppHttpAsyncResult(AsyncCallback cb, Object state) : base(cb, state) { }
    }

}

namespace System.Web
{
    public class HttpApp : HttpApplication, IHttpHandler, IHttpAsyncHandler
    {
        public bool IsReusable { get { return true; } }


        public static void InitInternal(HttpApplication app, HttpContext context)
        {
           (app as HttpApp).Init(context);
        }

        //     Enables processing of HTTP Web requests by a custom HttpHandler that implements
        //     the System.Web.IHttpHandler interface.
        public void ProcessRequest(HttpContext context)
        {
            //  base.ProcessRequest(context);
            BeginRequest(null);
        }

        //     Causes ASP.NET to bypass all events and filtering in the HTTP pipeline chain
        //     of execution and directly execute the System.Web.HttpApplication.EndRequest event.
        public new void CompleteRequest()
        {
            base.CompleteRequest();
        }

        public override void Dispose()
        {
            base.Dispose();
        }


        public // new 
            virtual void Init(HttpContext context)
        {
            //Application_Init();
        }
        public new virtual void BeginRequest(IAsyncResult async)
        {
            //Application_BeginRequest();
        }
        public new virtual void EndRequest()
        {
            //Application_EndRequest();
        }


        public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            var async = CheckWorker.BeginProcessRequest(context, CheckWorker.HandlerCompletionCallback, this);

            BeginRequest(async);
            return async;
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            var state = result.AsyncState as HttpContext;
            if (state == null || state.Error != null)
                return;

            EndRequest();

            // state.IsDebuggingEnabled = true;
            var items = state.Items;
            var response = state.Response;
            var request = state.Request;
            if (response.StatusCode != 200)
                return;

            var file = request.FilePath;
            response.Write(string.Format("<br/>file={0}", file));

            var query = request.QueryString;
            response.Write(string.Format("<br/>query={0}", query));

            // OK
        }
    }


    //internal 
    public class HttpAsyncResult : IAsyncResult
    {
        #region Properties

        private AsyncCallback _callback;
        private Object _asyncState;

        private bool _completed;
        private bool _completedSynchronously;

        private Object _result;
        private Exception _error;
        private Thread _threadWhichStartedOperation;

        // pipeline support
        private RequestNotificationStatus _status;

        #endregion

        /*
         * Constructor with pending result
         */
        internal HttpAsyncResult(AsyncCallback cb, Object state)
        {
            _callback = cb;
            _asyncState = state;
            _status = RequestNotificationStatus.Continue;
        }

        /*
         * Constructor with known result
         */
        // internal 
        public HttpAsyncResult(AsyncCallback cb, Object state,
                                 bool completed, Object result, Exception error)
        {
            _callback = cb;
            _asyncState = state;

            _completed = completed;
            _completedSynchronously = completed;

            _result = result;
            _error = error;
            _status = RequestNotificationStatus.Continue;

            if (_completed && _callback != null)
                _callback(this);
        }

        internal void SetComplete()
        {
            _completed = true;
        }

        /*
         * Helper method to process completions
         */
        internal void Complete(bool synchronous, Object result, Exception error, RequestNotificationStatus status)
        {
            //if (System.Threading.Volatile.Read(ref _threadWhichStartedOperation) == Thread.CurrentThread) {
            //    // If we're calling Complete on the same thread which kicked off the operation, then
            //    // we ignore the 'synchronous' value that the caller provided to us since we know
            //    // for a fact that this is really a synchronous completion. This is only checked if
            //    // the caller calls the MarkCallToBeginMethod* routines below.
            //    synchronous = true;
            //}

            _completed = true;
            _completedSynchronously = synchronous;
            _result = result;
            _error = error;
            _status = status;

            if (_callback != null)
                _callback(this);
        }

        internal void Complete(bool synchronous, Object result, Exception error)
        {
            Complete(synchronous, result, error, RequestNotificationStatus.Continue);
        }


        /*
         * Helper method to implement End call to async method
         */
        internal Object End()
        {
            if (_error != null)
                throw new HttpException(null, _error);

            return _result;
        }

        // If the caller needs to invoke an asynchronous method where the only way of knowing whether the
        // method actually completed synchronously is to inspect which thread the callback was invoked on,
        // then the caller should surround the asynchronous call with calls to the below Started / Completed
        // methods. The callback can compare the captured thread against the current thread to see if the
        // completion was synchronous. The caller calls the Completed method when unwinding so that the
        // captured thread can be cleared out, preventing an asynchronous invocation on the same thread
        // from being mistaken for a synchronous invocation.

        internal void MarkCallToBeginMethodStarted()
        {
            Thread originalThread = Interlocked.CompareExchange(ref _threadWhichStartedOperation, Thread.CurrentThread, null);
            //Debug.Assert(originalThread == null, "Another thread already called MarkCallToBeginMethodStarted.");
        }

        internal void MarkCallToBeginMethodCompleted()
        {
            Thread originalThread = Interlocked.Exchange(ref _threadWhichStartedOperation, null);
            //Debug.Assert(originalThread == Thread.CurrentThread, "This thread did not call MarkCallToBeginMethodStarted.");
        }

        //
        // Properties that are not part of IAsyncResult
        //

        internal Exception Error { get { return _error; } }

        internal RequestNotificationStatus Status {
            get {
                return _status;
            }
        }

        //
        // IAsyncResult implementation
        //

        public bool IsCompleted { get { return _completed; } }
        public bool CompletedSynchronously { get { return _completedSynchronously; } }
        public Object AsyncState { get { return _asyncState; } }
        public WaitHandle AsyncWaitHandle { get { return null; } } // wait not supported
    }



}

