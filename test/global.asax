<%@ Import Namespace="System.IO" %>
<script runat="server" language="c#" >
	static object appV2 = null;

	void Application_Start (object o, EventArgs args)
	{
		Console.WriteLine ("Application_Start");
	        Type type;

		//type = Type.GetType ("Samples.Application, App_Code", false);
		//if (type != null)
		//	appV2 = Activator.CreateInstance (type, new object[] {HttpContext.Current});
	}

	void Application_End (object o, EventArgs args)
	{
		Console.WriteLine ("Application_End");
	}

    	void Application_Error (object o, EventArgs args)
	{
		Console.WriteLine ("Error:");
	}

	void Application_BeginRequest (object o, EventArgs args)
	{
          var raw = Request.RawUrl ?? "";
          // if (raw.Length <= 2)
          //     Response.Redirect("/index.aspx");
	}
</script>
