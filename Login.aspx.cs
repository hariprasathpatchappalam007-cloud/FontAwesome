using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;

namespace TAMWEEL_RSU
{
 
 public partial class Login : System.Web.UI.Page
 {
        raqValidation RQ;

        private static void LogTrace(string message)
        {
            try
            {
                HttpContext context = HttpContext.Current;
                if (context == null)
                {
                    return;
                }

                string path = context.Server.MapPath("~/App_Data/LoginTrace.log");
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string sessionId = context.Session != null ? context.Session.SessionID : "(no session)";
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | SessionID=" + sessionId + " | " + message;
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch
            {
            }
        }

  protected void Page_Load(object sender, System.EventArgs e)
  {

   Response.CacheControl = "no-cache";
   Response.AddHeader("Pragma", "no-cache");
   Response.Expires = -1;

   LogTrace("Page_Load - Start. IsPostBack=" + IsPostBack + "; Session[USERID]=" + Convert.ToString(Session["USERID"]));

   if(IsPostBack)
    return;

   userName.ReadOnly = true ; //--T24<<SIMULATION
   // Put user code to initialize the page here

   //Session["USERID"] ="" ; //--T24<<SIMULATION

           

            //userName.Text = Session["USERID"] != null ? Convert.ToString(Session["USERID"]) : string.Empty; //--T24<<SIMULATION
            
            try
            {
                RQ = new raqValidation();
                

                if (!IsPostBack)
                {
                    userName.Text = RQ.LoginID();
                    //<<"GIS Observations << 23-Aug-2026 <<Hariprasath"
                     if(Request.QueryString["logout"] != null && Convert.ToString(Request.QueryString["logout"]).Equals("true", StringComparison.InvariantCultureIgnoreCase)){
                        Session["USERID"]= null; 
                     }
                    Session["COUNT"] = 3;
                }

                if (HttpContext.Current.Session["LICENCE"].ToString() == "N")
                {
                    Button1.Enabled = false;
                    loginError.Text = "Invalid License.... Contact System Admin";
                    LIC_TO.Text = "Swastik Technology";
                }
                else
                {
                    LIC_TO.Text = HttpContext.Current.Application["LIC_TO"].ToString();
                }
            }
            catch (Exception ex)
            {
                loginError.Text = ex.Message.ToString();
                Button1.Enabled = false;
            }
  }

  #region Web Form Designer generated code
  override protected void OnInit(EventArgs e)
  {
   //
   // CODEGEN: This call is required by the ASP.NET Web Form Designer.
   //
   InitializeComponent();
   base.OnInit(e);
  }
  
  /// <summary>
  /// Required method for Designer support - do not modify
  /// the contents of this method with the code editor.
  /// </summary>
  private void InitializeComponent()
  {    

  }
  #endregion

        protected void Button1_Click(object sender, System.EventArgs e)
        {
            raqValidation _raqValidation = new raqValidation();

            LogTrace("Button1_Click - Start. UserName=" + Convert.ToString(userName.Text));

            try
            {

                string AD_VALIDATION = _raqValidation.getAppSettings("AD_VALIDATION");
                string domainName = domainanme.SelectedValue.ToString();

                LogTrace("AD_VALIDATION=" + AD_VALIDATION + "; DomainName=" + domainName);

                Session["USERDOMAIN"] = domainName;
                int result;
              
                if (AD_VALIDATION == "Y")
                {
                    bool isAuthenticated = _raqValidation.IsAuthenticated(domainName, userName.Text, userPassword.Text);
                    LogTrace("IsAuthenticated=" + isAuthenticated);

                    if (isAuthenticated)
                    {
                        
                        result = _raqValidation.IsActiveUser(userName.Text);
                        LogTrace("IsActiveUser result=" + result);

                        if (result == 1)
                        {
                            Session["USERID"] = userName.Text.ToUpper();//<<"GIS Observations << 23-Aug-2026 <<Hariprasath"
                            LogTrace("Session[USERID] set to " + Convert.ToString(Session["USERID"]) + ". Redirecting to menu.aspx.");
                            _raqValidation.Audit_LOG("LOGIN", "Successfully Login", "0");
                            Response.Redirect("menu.aspx");
                        }
                        else if (result == 0)
                        {
                            _raqValidation.Audit_LOG("LOGIN", "UnSuccessfull Login - user account is disabled", "0");
                            loginError.Text = "User account is in-active, please contact IT ServiceDesk / ACU";
                        }
                        else if (result == -1)
                        {
                            _raqValidation.Audit_LOG("LOGIN", "UnSuccessfull Login - invalid user account", "0");
                            loginError.Text = "User account is invalid, please contact IT ServiceDesk / ACU";
                        }

                    }
                    else
                    {

                        if (Session["COUNT"] == null)
                            Session["COUNT"] = 3;

                        int NoofCounts = (int)Session["COUNT"];
                        NoofCounts = NoofCounts - 1;
                        Session["COUNT"] = NoofCounts;
                        if (NoofCounts <= 0)
                        {
                            _raqValidation.Audit_LOG("LOGIN", "UnSuccessfull Login, Account Locked", "2");
                            loginError.Text = "Account is locked, please contact Administrator";
                        }
                        else
                        {
                            _raqValidation.Audit_LOG("LOGIN", "UnSuccessfull Login", "1");
                            loginError.Text = "Invalid Password ! ( Account will be locked after " + NoofCounts.ToString() + " wrong attempts )";
                        }
                    }

                }
                else
                {
                  result = _raqValidation.IsActiveUser(userName.Text);
                   //result = _raqValidation.IsActiveUser(userName.Text, userPassword.Text); //--<<T24<SIMULATION
                    LogTrace("AD_VALIDATION=N path. IsActiveUser result=" + result);

                    if (result == 1)
                    {
                        _raqValidation.Audit_LOG("LOGIN", "Successfully Login", "0");

                        string sHostIP = Request.ServerVariables["REMOTE_HOST"];
                        //System.Net.IPHostEntry ipEntry = System.Net.Dns.GetHostByAddress(sHostIP);
                      //  System.Net.IPHostEntry ipEntry = System.Net.Dns.GetHostEntry(sHostIP);

                        Session["REMOTE_HOST_IP"] = sHostIP;
                        //Session["REMOTE_HOST_NAME"] = ipEntry.HostName;

                        Session["USERID"] = userName.Text.ToUpper();
                        //Session["USER_NAME"] = new raqValidation().UserDetails(userName.Text.ToUpper()).Tables[0].Rows[0]["Fname"].ToString();
                        //Server.Transfer("menu.aspx");
                        //Server.Transfer("default.aspx");

                        LogTrace("Session[USERID] set to " + Convert.ToString(Session["USERID"]) + ". Redirecting to menu.aspx.");
                        Response.Redirect("menu.aspx");

                    }
                    else if(result == 0)
                    {
                        _raqValidation.Audit_LOG("LOGIN", "UnSuccessfull Login - user account is disabled", "0");
                        loginError.Text = "User account is in-active, please contact IT ServiceDesk / ACU";
                    }
                    else if (result == -1)
                    {
                        _raqValidation.Audit_LOG("LOGIN", "UnSuccessfull Login - invalid user account", "0");
                        loginError.Text = "User account is invalid, please contact IT ServiceDesk / ACU";
                    }

                }
            }
            catch (Exception ex)
            {
                LogTrace("Exception: " + ex.Message);
                loginError.Text = ex.Message.ToString();
            }

        }
        
        
      
 }
}
 