<%@ Page Language="C#" %>
<%@ Import Namespace="System.Web" %>

<%
    Response.Write("Version: " + System.Environment.Version.ToString());

    Response.Write("<br/>UsingIntegratedPipeline=" + System.Web.HttpRuntime.UsingIntegratedPipeline.ToString());
    // Response.Write(", IISVersion=" + System.Web.HttpRuntime.IISVersion.ToString());
    Response.Write("<br/>AspInstallDirectory=" + System.Web.HttpRuntime.AspInstallDirectory.ToString());
    Response.Write("<br/>AppDomain.BaseDirectory=" + System.AppDomain.CurrentDomain.BaseDirectory);

    Response.Write("<br/>Assemblies=" + System.AppDomain.CurrentDomain.GetAssemblies().Length.ToString());

    foreach (var asm in System.Linq.Enumerable.OrderBy(System.AppDomain.CurrentDomain.GetAssemblies(),
        a => a.IsDynamic ? a.FullName : a.CodeBase))
    {
        try {
            Response.Write("<br/>" + asm.CodeBase.Replace("file:///", ""));
        }
        catch {
            // case: Anonymously Hosted DynamicMethods Assembly
            Response.Write("<br/>" + asm.FullName);
        }
    }

%>