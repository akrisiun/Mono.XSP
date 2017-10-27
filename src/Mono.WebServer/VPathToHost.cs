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

		public VPathToHost (string vhost, int vport, string vpath, string realPath)
		{
			this.vhost = (vhost != null) ? vhost.ToLower (CultureInfo.InvariantCulture) : null;
			this.vport = vport;
			this.vpath = vpath;
			if (String.IsNullOrEmpty (vpath) || vpath [0] != '/')
				throw new ArgumentException ("Virtual path must begin with '/': " + vpath,
							     "vpath");

			this.realPath = realPath;
			AppHost = null;
			if (vhost != null && this.vhost.Length != 0 && this.vhost [0] == '*') {
				haveWildcard = true;
				if (this.vhost.Length > 2 && this.vhost [1] == '.')
					this.vhost = this.vhost.Substring (2);
			}
		}

		public bool TryClearHost (IApplicationHost host)
		{
			if (AppHost == host) {
				AppHost = null;
				return true;
			}

			return false;
		}

		public void UnloadHost ()
		{
			if (AppHost != null)
				AppHost.Unload ();

			AppHost = null;
		}

		public bool Redirect (string path, out string redirect)
		{
			redirect = null;
			if (path.Length == vpath.Length - 1) {
				redirect = vpath;
				return true;
			}

			return false;
		}

		public bool Match (string vhost, int vport, string vpath)
		{
			if (vport != -1 && this.vport != -1 && vport != this.vport)
				return false;

			if (vpath == null)
				return false;

			if (vhost != null && this.vhost != null && this.vhost != "*") {
				int length = this.vhost.Length;
				string lwrvhost = vhost.ToLower (CultureInfo.InvariantCulture);
				if (haveWildcard) {
					if (length > vhost.Length)
						return false;

					if (length == vhost.Length && this.vhost != lwrvhost)
						return false;

					if (vhost [vhost.Length - length - 1] != '.')
						return false;

					if (!lwrvhost.EndsWith (this.vhost))
						return false;

				} else if (this.vhost != lwrvhost) {
					return false;
				}
			}

			int local = vpath.Length;
			int vlength = this.vpath.Length;
			if (vlength > local) {
				// Check for /xxx requests to be redirected to /xxx/
				if (this.vpath [vlength - 1] != '/')
					return false;

				return (vlength - 1 == local && this.vpath.Substring (0, vlength - 1) == vpath);
			}

			return (vpath.StartsWith (this.vpath));
		}

		public void CreateHost (ApplicationServer server, WebSource webSource)
		{
			string v = vpath;
			if (v != "/" && v.EndsWith ("/")) {
				v = v.Substring (0, v.Length - 1);
			}

            var domain = AppDomain.CurrentDomain;
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
            } catch (Exception err) { error = err.InnerException ?? err; }

            if (AppHost == null && error != null)
            {
                Console.WriteLine(error.Message);
                if (Debugger.IsAttached)
                    Debugger.Break();

                Console.ReadKey();
                throw error;
            }

            if (!server.SingleApplication) {
				// Link the host in the application domain with a request broker in the main domain
				RequestBroker = webSource.CreateRequestBroker ();
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
            AppDomain appdomain;
            appdomain = AppDomain.CreateDomain(domain_id, evidence, setup);

            //
            // Populate with the AppDomain data keys expected, Mono only uses a
            // few, but third party apps might use others:
            //
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
            } catch (Exception err) { appdomain.SetData("load.Error", err); }

            return appdomain.CreateInstanceAndUnwrap(hostType.Module.Assembly.FullName, hostType.FullName);
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

