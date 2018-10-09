namespace Sitecore.Support.Forms.Core.Commands
{
  using Sitecore;
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Form.Core.Configuration;
  using Sitecore.Shell.Framework.Commands;
  using Sitecore.Text;
  using Sitecore.Web;
  using Sitecore.Web.UI.Sheer;
  using Sitecore.Web.UI.XamlSharp.Continuations;
  using Sitecore.WFFM.Abstractions.Dependencies;
  using System;
  using System.Collections.Specialized;

  [Serializable]
  public class InsertForm : Command, ISupportsContinuation
  {
    public static readonly string messageError = string.Empty;
    private string itemShortID;

    private void AddNonEmptyUrlParam(UrlString url, string name, string value)
    {
      Assert.ArgumentNotNull(url, "url");
      Assert.ArgumentNotNull(name, "name");
      if (!string.IsNullOrEmpty(value))
      {
        url.Add(name, value);
      }
    }

    public override void Execute(CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");
      NameValueCollection parameters = new NameValueCollection();
      this.SetNonEmptyContextParam(parameters, context, "placeholder");
      this.SetNonEmptyContextParam(parameters, context, "url");
      if (context.Parameters["mode"] == StaticSettings.DesignMode)
      {
        parameters["deviceid"] = ShortID.Decode(WebUtil.GetFormValue("scDeviceID"));
        parameters["id"] = ShortID.Decode(WebUtil.GetFormValue("scItemID"));
        parameters["db"] = Sitecore.Client.ContentDatabase.Name;
        parameters["la"] = Sitecore.Client.Site.Language;
        parameters["vs"] = string.Empty;
        parameters["mode"] = StaticSettings.DesignMode;
        Context.ClientPage.Start(this, "Run", parameters);
      }
      else if (context.Items.Length == 1)
      {
        Item item = context.Items[0];
        parameters["id"] = item.ID.ToString();
        parameters["la"] = item.Language.Name;
        parameters["vs"] = item.Version.Number.ToString();
        parameters["db"] = item.Database.Name;
        this.SetNonEmptyContextParam(parameters, context, "mode");
        Context.ClientPage.Start(this, "Run", parameters);
        itemShortID = item.ID.ToShortID().ToString();
      }
    }

    private string FindDevice(Database database, string queryparam)
    {
      Assert.ArgumentNotNull(database, "database");
      Assert.ArgumentNotNull(queryparam, "queryparam");
      Item item = database.GetItem(ItemIDs.DevicesRoot);
      if (item != null)
      {
        foreach (Item item2 in item.Children)
        {
          if (item2[DeviceFieldIDs.QueryString] == queryparam)
          {
            return item2.ID.ToString();
          }
        }
      }
      return string.Empty;
    }

    private string GetDevice(ClientPipelineArgs args, Database database)
    {
      Assert.ArgumentNotNull(args, "args");
      Assert.ArgumentNotNull(database, "database");
      string str = args.Parameters["deviceid"];
      if (!string.IsNullOrEmpty(str))
      {
        return str;
      }
      string str2 = args.Parameters["url"];
      if (string.IsNullOrEmpty(str2))
      {
        return string.Empty;
      }
      UrlString str3 = new UrlString(str2);
      return this.FindDevice(database, "p=" + str3.Parameters["p"]);
    }

    public override CommandState QueryState(CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");
      if (context.Items.Length != 0)
      {
        Item item = context.Items[0];
        if (!item.Security.CanWrite(Context.User) || item.Appearance.ReadOnly)
        {
          return CommandState.Disabled;
        }
      }
      return base.QueryState(context);
    }

    protected void Run(ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      if (SheerResponse.CheckModified())
      {
        if (!args.IsPostBack)
        {
          this.ShowWizard(args);
        }
        else if (args.HasResult)
        {
          CommandContext context = new CommandContext(Database.GetItem(ItemUri.Parse(args.Result)));
          CommandManager.GetCommand("forms:designer").Execute(context);
        }
        //Patch start Sitecore.Support.257747
        else
        {
          SheerResponse.Eval("scForm.postRequest(\"\", \"\", \"\", \"LoadItem(\\\"" + itemShortID + "\\\")\")");
        }
        //Patch end Sitecore.Support.257747
      }
    }

    private void SetNonEmptyContextParam(NameValueCollection parameters, CommandContext context, string name)
    {
      Assert.ArgumentNotNull(parameters, "parameters");
      Assert.ArgumentNotNull(context, "context");
      Assert.ArgumentNotNull(name, "name");
      string str = context.Parameters[name];
      if (!string.IsNullOrEmpty(str))
      {
        parameters[name] = str;
      }
    }

    private void ShowWizard(ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      string path = args.Parameters["id"];
      string name = args.Parameters["db"];
      Database database = Factory.GetDatabase(name);
      if (string.IsNullOrEmpty(database.GetItem(path)["__renderings"]))
      {
        Context.ClientPage.ClientResponse.Alert(DependenciesManager.ResourceManager.Localize("HAS_NO_LAYOUT"));
      }
      else
      {
        UrlString url = new UrlString(UIUtil.GetUri("control:Forms.InsertFormWizard"));
        url.Add("id", path);
        url.Add("db", name);
        url.Add("la", args.Parameters["la"]);
        url.Add("vs", args.Parameters["vs"]);
        this.AddNonEmptyUrlParam(url, "placeholder", args.Parameters["placeholder"]);
        this.AddNonEmptyUrlParam(url, "mode", args.Parameters["mode"]);
        this.AddNonEmptyUrlParam(url, "deviceid", this.GetDevice(args, database));
        Context.ClientPage.ClientResponse.ShowModalDialog(url.ToString(), true);
        args.WaitForPostBack();       
      }
    }
  }
}
