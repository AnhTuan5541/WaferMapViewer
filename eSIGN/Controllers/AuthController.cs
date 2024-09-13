using WaferMapViewer.Common;
using WaferMapViewer.Data;
using WaferMapViewer.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.DirectoryServices;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;

namespace WaferMapViewer.Controllers
{
    public class AuthController : Controller
    {
        private readonly ConnectionStrings _connection;
        public AuthController(IOptions<ConnectionStrings> connection) { _connection = connection.Value; }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GetUserInfo(string userid, string password, string site)
        {
            string functionName = ControllerContext.ActionDescriptor.ControllerName + "/" + System.Reflection.MethodBase.GetCurrentMethod().Name;
            try
            {
                
                if (string.IsNullOrEmpty(userid) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(site))
                {
                    CommonFunction.LogInfo(_connection.DefaultConnection, userid, "Fail to Sign in: Empty username or password", CommonFunction.SUCCESS, functionName);
                    var response = new CommonResponse<Dictionary<string, object>>
                    {
                        StatusCode = CommonFunction.FAIL,
                        Message = "Fail to Sign in: Empty username or password",
                        Data = null,
                        size = 0
                    };
                    return Ok(response);
                }
                else {
                    Dictionary<string, object> userInfo = new Dictionary<string, object>();
                    List<Dictionary<string, object>> userList = new List<Dictionary<string, object>>();
                    //Kiểm tra tài khoàn AD
                    string ldapPathFormat = string.Empty;
                    switch (site)
                    {
                        case "ATK":
                            ldapPathFormat = "LDAP://10.141.10.23:3268";//ldap://k5wkrdcp01.kr.ds.amkor.com:3268
                            break;
                        case "ATI":
                            ldapPathFormat = "LDAP://AWUSDCP04.us.ds.amkor.com:3268";//AWUSDCP05.us.ds.amkor.com
                            break;
                        case "ATJ":
                            ldapPathFormat = "LDAP://Adldap.amkor.com:3268";
                            break;
                        case "ATC":
                            ldapPathFormat = "LDAP://C3WCNDCP01.CN.DS.AMKOR.COM:3268";//LDAP://C3WCNDCP02.CN.DS.AMKOR.COM:3268
                            break;
                        case "ATM":
                            ldapPathFormat = "LDAP://MWMYDCP01.MY.DS.AMKOR.COM:3268";
                            break;
                        case "ATP":
                            ldapPathFormat = "LDAP://P1WPHDCP01.PH.DS.AMKOR.COM:3268";
                            break;
                        case "ATT":
                            ldapPathFormat = "LDAP://T1WTWDCP01.TW.DS.AMKOR.COM:3268";
                            break;
                        case "ATEP":
                            ldapPathFormat = "LDAP://PTWDCP01.eu.ds.amkor.com:3268";
                            break;
                        case "ATV":
                            ldapPathFormat = "LDAP://AWVNDCP01.vn.ds.amkor.com:3268";
                            /*ldapPathFormat = "LDAP://V1WVNDCP01.vn.ds.amkor.com:3268";*/
                            break;
                    }
                    System.DirectoryServices.DirectoryEntry objDirEntry = new System.DirectoryServices.DirectoryEntry(ldapPathFormat, userid, password);
                    DirectorySearcher search = new DirectorySearcher(objDirEntry);
                    search.Filter = "(samaccountname=" + userid + ")";
                    SearchResult users = search.FindOne();
                    if (users != null)
                    {
                        //--Lấy thông tin tài khoản
                        //string empID = users.Properties["employeenumber"].Cast<Object>().FirstOrDefault().ToString();
                        string surName = users.Properties["sn"].Cast<Object>().FirstOrDefault().ToString();
                        string lastName = users.Properties["givenname"].Cast<Object>().FirstOrDefault().ToString();
                        string fullName = surName + " " + lastName;
                        string displayName = users.Properties["displayname"].Cast<Object>().FirstOrDefault().ToString();
                        string department = users.Properties["department"].Cast<Object>().FirstOrDefault().ToString();
                        //string company = users.Properties["company"].Cast<Object>().FirstOrDefault().ToString();
                        string company = "";
                        string titleName = users.Properties["title"].Cast<Object>().FirstOrDefault().ToString().Split(',')[0];
                        string email = users.Properties["mail"].Cast<Object>().FirstOrDefault().ToString();

                        //SQL
                        using var connection = new SqlConnection(_connection.DefaultConnection);

                        using var command2 = new SqlCommand("GetRoleByIdCard", connection) { CommandType = CommandType.StoredProcedure };
                        command2.Parameters.AddWithValue("@idCard", userid);

                        connection.Open();
                        var reader2 = command2.ExecuteReader();
                        List<Dictionary<string, object>> data2 = CommonFunction.GetDataFromProcedure(reader2);
                        connection.Close();

                        userInfo.Add("user_id", userid);
                        userInfo.Add("display_name", displayName);
                        userInfo.Add("title_name", titleName);
                        userInfo.Add("email", email);
                        userInfo.Add("department", department);
                        userInfo.Add("company", company);
                        userInfo.Add("role", data2);
                        userInfo.Add("token", CommonFunction.GenerateToken(userid));
                        userList.Add(userInfo);

                        using var command = new SqlCommand("AddUserInfo", connection) { CommandType = CommandType.StoredProcedure };
                        command.Parameters.AddWithValue("@idCard", userid);
                        command.Parameters.AddWithValue("@name", fullName);
                        command.Parameters.AddWithValue("@email", email);
                        command.Parameters.AddWithValue("@title", titleName);
                        command.Parameters.AddWithValue("@department", department);
                        command.Parameters.AddWithValue("@company", company);
                        command.Parameters.AddWithValue("@displayName", displayName);

                        connection.Open();
                        var reader = command.ExecuteReader();
                        connection.Close();

                        CommonFunction.LogInfo(_connection.DefaultConnection, userid, "Login successfully", CommonFunction.SUCCESS, functionName);
                        var response = new CommonResponse<Dictionary<string, object>>
                        {
                            StatusCode = CommonFunction.SUCCESS,
                            Message = "Login successfully",
                            Data = userList,
                            size = userList.Count()
                        };
                        return Ok(response);

                    }
                    else
                    {
                        CommonFunction.LogInfo(_connection.DefaultConnection, userid, "Login fail. User not exist!", CommonFunction.FAIL, functionName);
                        var response = new CommonResponse<Dictionary<string, object>>
                        {
                            StatusCode = CommonFunction.FAIL,
                            Message = "Login fail. User not exist!",
                            Data = null,
                            size = 0
                        };
                        return Ok(response);
                    }
                }
            }
            catch (Exception ex)
            {
                // Xử lý lỗi (ví dụ: log lỗi, trả về phản hồi lỗi)
                CommonFunction.LogInfo(_connection.DefaultConnection, userid, ex.Message, CommonFunction.ERROR, functionName);

                //try lan 2 thu bang ldap ATV khac
                try
                {
                    Dictionary<string, object> userInfo = new Dictionary<string, object>();
                    List<Dictionary<string, object>> userList = new List<Dictionary<string, object>>();
                    //Kiểm tra tài khoàn AD
                    string ldapPathFormat = string.Empty;
                    switch (site)
                    {
                        case "ATK":
                            ldapPathFormat = "LDAP://10.141.10.23:3268";//ldap://k5wkrdcp01.kr.ds.amkor.com:3268
                            break;
                        case "ATI":
                            ldapPathFormat = "LDAP://AWUSDCP04.us.ds.amkor.com:3268";//AWUSDCP05.us.ds.amkor.com
                            break;
                        case "ATJ":
                            ldapPathFormat = "LDAP://Adldap.amkor.com:3268";
                            break;
                        case "ATC":
                            ldapPathFormat = "LDAP://C3WCNDCP01.CN.DS.AMKOR.COM:3268";//LDAP://C3WCNDCP02.CN.DS.AMKOR.COM:3268
                            break;
                        case "ATM":
                            ldapPathFormat = "LDAP://MWMYDCP01.MY.DS.AMKOR.COM:3268";
                            break;
                        case "ATP":
                            ldapPathFormat = "LDAP://P1WPHDCP01.PH.DS.AMKOR.COM:3268";
                            break;
                        case "ATT":
                            ldapPathFormat = "LDAP://T1WTWDCP01.TW.DS.AMKOR.COM:3268";
                            break;
                        case "ATEP":
                            ldapPathFormat = "LDAP://PTWDCP01.eu.ds.amkor.com:3268";
                            break;
                        case "ATV":
                            /*ldapPathFormat = "LDAP://AWVNDCP01.vn.ds.amkor.com:3268";*/
                            ldapPathFormat = "LDAP://V1WVNDCP01.vn.ds.amkor.com:3268";
                            break;
                    }
                    System.DirectoryServices.DirectoryEntry objDirEntry = new System.DirectoryServices.DirectoryEntry(ldapPathFormat, userid, password);
                    DirectorySearcher search = new DirectorySearcher(objDirEntry);
                    search.Filter = "(samaccountname=" + userid + ")";
                    SearchResult users = search.FindOne();
                    if (users != null)
                    {
                        //--Lấy thông tin tài khoản
                        //string empID = users.Properties["employeenumber"].Cast<Object>().FirstOrDefault().ToString();
                        string surName = users.Properties["sn"].Cast<Object>().FirstOrDefault().ToString();
                        string lastName = users.Properties["givenname"].Cast<Object>().FirstOrDefault().ToString();
                        string fullName = surName + " " + lastName;
                        string displayName = users.Properties["displayname"].Cast<Object>().FirstOrDefault().ToString();
                        string department = users.Properties["department"].Cast<Object>().FirstOrDefault().ToString();
                        //string company = users.Properties["company"].Cast<Object>().FirstOrDefault().ToString();
                        string company = "";
                        string titleName = users.Properties["title"].Cast<Object>().FirstOrDefault().ToString().Split(',')[0];
                        string email = users.Properties["mail"].Cast<Object>().FirstOrDefault().ToString();

                        //SQL
                        using var connection = new SqlConnection(_connection.DefaultConnection);

                        using var command2 = new SqlCommand("GetRoleByIdCard", connection) { CommandType = CommandType.StoredProcedure };
                        command2.Parameters.AddWithValue("@idCard", userid);

                        connection.Open();
                        var reader2 = command2.ExecuteReader();
                        List<Dictionary<string, object>> data2 = CommonFunction.GetDataFromProcedure(reader2);
                        connection.Close();

                        userInfo.Add("user_id", userid);
                        userInfo.Add("display_name", displayName);
                        userInfo.Add("title_name", titleName);
                        userInfo.Add("email", email);
                        userInfo.Add("department", department);
                        userInfo.Add("company", company);
                        userInfo.Add("role", data2);
                        userInfo.Add("token", CommonFunction.GenerateToken(userid));
                        userList.Add(userInfo);

                        using var command = new SqlCommand("AddUserInfo", connection) { CommandType = CommandType.StoredProcedure };
                        command.Parameters.AddWithValue("@idCard", userid);
                        command.Parameters.AddWithValue("@name", fullName);
                        command.Parameters.AddWithValue("@email", email);
                        command.Parameters.AddWithValue("@title", titleName);
                        command.Parameters.AddWithValue("@department", department);
                        command.Parameters.AddWithValue("@company", company);
                        command.Parameters.AddWithValue("@displayName", displayName);

                        connection.Open();
                        var reader = command.ExecuteReader();
                        connection.Close();

                        CommonFunction.LogInfo(_connection.DefaultConnection, userid, "Login successfully", CommonFunction.SUCCESS, functionName);
                        var response = new CommonResponse<Dictionary<string, object>>
                        {
                            StatusCode = CommonFunction.SUCCESS,
                            Message = "Login successfully",
                            Data = userList,
                            size = userList.Count()
                        };
                        return Ok(response);

                    }
                    else
                    {
                        CommonFunction.LogInfo(_connection.DefaultConnection, userid, "Login fail. User not exist!", CommonFunction.FAIL, functionName);
                        var response = new CommonResponse<Dictionary<string, object>>
                        {
                            StatusCode = CommonFunction.FAIL,
                            Message = "Login fail. User not exist!",
                            Data = null,
                            size = 0
                        };
                        return Ok(response);
                    }
                }
                catch(Exception ex2) {
                    // Xử lý lỗi (ví dụ: log lỗi, trả về phản hồi lỗi)
                    CommonFunction.LogInfo(_connection.DefaultConnection, userid, ex.Message, CommonFunction.ERROR, functionName);

                    var errorResponse = new CommonResponse<User>
                    {
                        StatusCode = CommonFunction.FAIL,
                        Message = ex2.Message,
                        Data = null,
                        size = 0
                    };
                    return StatusCode(500, errorResponse);
                }

                // co the return errorResponse
            }
        }

        [HttpPost]
        public SearchResult GetFullUserInfo(string userid, string password, string site)
        {
            try
            {
                Dictionary<string, object> userInfo = new Dictionary<string, object>();
                List<Dictionary<string, object>> userList = new List<Dictionary<string, object>>();
                if (string.IsNullOrEmpty(userid) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(site))
                {
                    return null;
                }
                else
                {
                    //Kiểm tra tài khoàn AD
                    string ldapPathFormat = string.Empty;
                    switch (site)
                    {
                        case "ATK":
                            ldapPathFormat = "LDAP://10.141.10.23:3268";//ldap://k5wkrdcp01.kr.ds.amkor.com:3268
                            break;
                        case "ATI":
                            ldapPathFormat = "LDAP://AWUSDCP04.us.ds.amkor.com:3268";//AWUSDCP05.us.ds.amkor.com
                            break;
                        case "ATJ":
                            ldapPathFormat = "LDAP://Adldap.amkor.com:3268";
                            break;
                        case "ATC":
                            ldapPathFormat = "LDAP://C3WCNDCP01.CN.DS.AMKOR.COM:3268";//LDAP://C3WCNDCP02.CN.DS.AMKOR.COM:3268
                            break;
                        case "ATM":
                            ldapPathFormat = "LDAP://MWMYDCP01.MY.DS.AMKOR.COM:3268";
                            break;
                        case "ATP":
                            ldapPathFormat = "LDAP://P1WPHDCP01.PH.DS.AMKOR.COM:3268";
                            break;
                        case "ATT":
                            ldapPathFormat = "LDAP://T1WTWDCP01.TW.DS.AMKOR.COM:3268";
                            break;
                        case "ATEP":
                            ldapPathFormat = "LDAP://PTWDCP01.eu.ds.amkor.com:3268";
                            break;
                        case "ATV":
                            ldapPathFormat = "LDAP://AWVNDCP01.vn.ds.amkor.com:3268";/*//LDAP://V1WVNDCP01.vn.ds.amkor.com:3268*/
                            break;
                    }
                    System.DirectoryServices.DirectoryEntry objDirEntry = new System.DirectoryServices.DirectoryEntry(ldapPathFormat, userid, password);
                    DirectorySearcher search = new DirectorySearcher(objDirEntry);
                    search.Filter = "(samaccountname=" + userid + ")";
                    SearchResult users = search.FindOne();
                    return users;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
