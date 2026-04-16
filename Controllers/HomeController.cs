using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TechVision.Helpers;
using TechVision.Models;


namespace TechVision.Controllers
{
    public class HomeController : Controller
    {
        string cs = ConfigurationManager
            .ConnectionStrings["DefaultConnection"].ConnectionString;

        // ================= HOME =================
        public ActionResult Index()
        {
            return View();
        }
        // ================= ABOUT US =================
        public ActionResult About()
        {
            return View();
        }

        // ================= SERVICES =================
        public ActionResult Service()
        {
            return View();
        }

        // ================= CONTACT =================
        public ActionResult Contact()
        {
            return View();
        }


        // ================= SIGN UP =================
        public ActionResult Sign_up()
        {
            return View();
        }


        [HttpPost]
        public ActionResult Sign_up(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string Confirm_Password)
        {
            // 1. Password match validation
            if (Password != Confirm_Password)
            {
                ViewBag.Message = "Passwords do not match!";
                return View();
            }

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // 2. Check if email already exists
                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Email=@Email";
                SqlCommand checkCmd = new SqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@Email", Email);

                int count = (int)checkCmd.ExecuteScalar();

                if (count > 0)
                {
                    ViewBag.Message = "Email already registered!";
                    return View();
                }

                // 3. Insert user
                SqlCommand cmd = new SqlCommand("sp_InsertUser", con);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@FirstName", FirstName);
                cmd.Parameters.AddWithValue("@LastName", LastName);
                cmd.Parameters.AddWithValue("@Email", Email);
                string hashedPassword = PasswordHelper.HashPassword(Password);
                cmd.Parameters.AddWithValue("@Password", hashedPassword);


                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Account created";
            return RedirectToAction("Sign_up");

        }


        // ================= SIGN IN =================
        public ActionResult Sign_in()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Sign_in(string Email, string Password)
        {
            using (SqlConnection con = new SqlConnection(cs))
            {
                SqlCommand cmd = new SqlCommand("sp_LoginUser", con);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                string hashedPassword = PasswordHelper.HashPassword(Password);

                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@Password", hashedPassword);


                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    Session["UserId"] = dr["Id"];
                    Session["UserName"] = dr["FirstName"] + " " + dr["LastName"];
                    Session["UserEmail"] = dr["Email"];
                    Session["IsAdmin"] = dr["IsAdmin"];

                    if (dr["ProfilePhoto"] != DBNull.Value)
                        Session["ProfilePhoto"] = dr["ProfilePhoto"].ToString();
                    else
                        Session["ProfilePhoto"] = "/Uploads/default.png";

                    // 👇 ADMIN REDIRECT
                    if (Convert.ToBoolean(dr["IsAdmin"]))
                        return RedirectToAction("AdminDashboard");

                    return RedirectToAction("Dashboard");
                }


                ViewBag.Message = "Invalid Email or Password";
                return View();
            }
        }

        // ================= DASHBOARD =================
        public ActionResult Dashboard()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Sign_in");

            ViewBag.UserName = Session["UserName"];
            ViewBag.UserEmail = Session["UserEmail"];
            ViewBag.ProfilePhoto = Session["ProfilePhoto"];

            return View();
        }

        // ================= EDIT PROFILE =================
        public ActionResult EditProfile()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Sign_in");

            ViewBag.Error = TempData["Error"];
            return View();
        }

        [HttpPost]
        public ActionResult EditProfile(string Address, DateTime? DOB, HttpPostedFileBase ProfilePhoto)
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Sign_in");

            int userId = Convert.ToInt32(Session["UserId"]);
            string photoPath = null;

            // ===== FILE VALIDATION =====
            if (ProfilePhoto != null && ProfilePhoto.ContentLength > 0)
            {
                int maxSize = 2 * 1024 * 1024; // 2MB
                if (ProfilePhoto.ContentLength > maxSize)
                {
                    TempData["Error"] = "Image must be less than 2 MB.";
                    return RedirectToAction("EditProfile");
                }

                string[] allowedExt = { ".jpg", ".jpeg", ".png" };
                string ext = Path.GetExtension(ProfilePhoto.FileName).ToLower();

                if (!allowedExt.Contains(ext))
                {
                    TempData["Error"] = "Only JPG and PNG images allowed.";
                    return RedirectToAction("EditProfile");
                }

                string fileName = Guid.NewGuid() + ext;
                photoPath = "/Uploads/" + fileName;
                ProfilePhoto.SaveAs(Server.MapPath(photoPath));
            }

            // ===== UPDATE DATABASE =====
            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"UPDATE Users
                                 SET Address = @Address,
                                     DOB = @DOB,
                                     ProfilePhoto = ISNULL(@ProfilePhoto, ProfilePhoto)
                                 WHERE Id = @Id";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Address", Address);
                cmd.Parameters.AddWithValue("@DOB", (object)DOB ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ProfilePhoto", (object)photoPath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Id", userId);

                con.Open();
                cmd.ExecuteNonQuery();
            }

            if (photoPath != null)
                Session["ProfilePhoto"] = photoPath;

            return RedirectToAction("Dashboard");
        }

        // ================= LOGOUT =================
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Sign_in");
        }
        // ================= ADMIN DASHBOARD =================
        public ActionResult AdminDashboard()
        {
            if (Session["IsAdmin"] == null || !(bool)Session["IsAdmin"])
                return RedirectToAction("Sign_in");

            return View();
        }

        // ================= VIEW ALL USERS =================
        public ActionResult ManageUsers()
        {
            if (Session["IsAdmin"] == null || !(bool)Session["IsAdmin"])
                return RedirectToAction("Sign_in");

            List<UserViewModel> users = new List<UserViewModel>();

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "SELECT * FROM Users";
                SqlCommand cmd = new SqlCommand(query, con);

                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    users.Add(new UserViewModel
                    {
                        Id = Convert.ToInt32(dr["Id"]),
                        Name = dr["FirstName"] + " " + dr["LastName"],
                        Email = dr["Email"].ToString(),
                        Address = dr["Address"] == DBNull.Value ? "" : dr["Address"].ToString(),
                        DOB = dr["DOB"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["DOB"])
                    });
                }
            }

            return View(users);
        }



        // ================= DELETE USER =================
        public ActionResult DeleteUser(int id)
        {
            if (Session["IsAdmin"] == null || !(bool)Session["IsAdmin"])
                return RedirectToAction("Sign_in");

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "DELETE FROM Users WHERE Id=@Id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", id);

                con.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("ManageUsers");
        }

        // ================= EDIT USER (GET) =================
        public ActionResult EditUser(int id)
        {
            if (Session["IsAdmin"] == null || !(bool)Session["IsAdmin"])
                return RedirectToAction("Sign_in");

            UserViewModel user = new UserViewModel();

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "SELECT * FROM Users WHERE Id=@id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@id", id);

                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    user.Id = Convert.ToInt32(dr["Id"]);
                    user.Name = dr["FirstName"] + " " + dr["LastName"];
                    user.Email = dr["Email"].ToString();
                    user.Address = dr["Address"] == DBNull.Value ? "" : dr["Address"].ToString();
                    user.DOB = dr["DOB"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["DOB"]);
                }
            }

            return View(user);
        }

        // ================= EDIT USER (POST) =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditUser(UserViewModel model)
        {
            if (Session["IsAdmin"] == null || !(bool)Session["IsAdmin"])
                return RedirectToAction("Sign_in");

            if (!ModelState.IsValid)
                return View(model);

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"UPDATE Users 
                     SET FirstName=@FirstName,
                         LastName=@LastName,
                         Email=@Email,
                         Address=@Address,
                         DOB=@DOB
                     WHERE Id=@Id";

                SqlCommand cmd = new SqlCommand(query, con);

                string[] parts = model.Name.Split(' ');

                cmd.Parameters.AddWithValue("@FirstName", parts[0]);
                cmd.Parameters.AddWithValue("@LastName", parts.Length > 1 ? parts[1] : "");
                cmd.Parameters.AddWithValue("@Email", model.Email);
                cmd.Parameters.AddWithValue("@Address", model.Address);
                cmd.Parameters.AddWithValue("@DOB", (object)model.DOB ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Id", model.Id);

                con.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "User updated successfully!";
            return RedirectToAction("ManageUsers");
        }
        // ================= PRINT SINGLE USER =================
        public ActionResult PrintUser(int id)
        {
            if (Session["IsAdmin"] == null || !(bool)Session["IsAdmin"])
                return RedirectToAction("Sign_in");

            UserViewModel user = new UserViewModel();

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "SELECT * FROM Users WHERE Id=@id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@id", id);

                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    user.Id = Convert.ToInt32(dr["Id"]);
                    user.Name = dr["FirstName"] + " " + dr["LastName"];
                    user.Email = dr["Email"].ToString();
                    user.Address = dr["Address"] == DBNull.Value ? "" : dr["Address"].ToString();
                    user.DOB = dr["DOB"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["DOB"]);
                }
            }

            return View(user);
        }
        // ================= REGISTER EMPLOYEE (GET) =================
        public ActionResult RegisterEmployee()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Sign_in");

            ViewBag.Error = TempData["Error"];
            return View();
        }

        // ================= REGISTER EMPLOYEE (POST) =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegisterEmployee(EmployeeViewModel model)
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Sign_in");

            // 🔹 1. Model Validation
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // 🔹 2. Duplicate Email Check
                string checkQuery = "SELECT COUNT(*) FROM Employees WHERE Email=@Email";
                SqlCommand checkCmd = new SqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@Email", model.Email);

                int count = (int)checkCmd.ExecuteScalar();

                if (count > 0)
                {
                    ModelState.AddModelError("", "Employee with this email already exists!");
                    return View(model);
                }

                // 🔹 3. Insert using Stored Procedure
                SqlCommand cmd = new SqlCommand("sp_InsertEmployee", con);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@FullName", model.FullName);
                cmd.Parameters.AddWithValue("@Address", model.Address);
                cmd.Parameters.AddWithValue("@CompanyName", model.CompanyName);
                cmd.Parameters.AddWithValue("@Salary", model.Salary);
                cmd.Parameters.AddWithValue("@MaritalStatus", model.MaritalStatus);
                cmd.Parameters.AddWithValue("@Email", model.Email);
                cmd.Parameters.AddWithValue("@Gender", model.Gender);
                cmd.Parameters.AddWithValue("@Mobile", model.Mobile);

                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Error: " + ex.Message;
                    return View(model);
                }
            }

            if (model.Salary < 0)
            {
                ModelState.AddModelError("Salary", "Salary cannot be negative");
                return View(model);
            }
            TempData["Success"] = "Employee Registered Successfully 🎉";
            return RedirectToAction("Dashboard");
        }
        public ActionResult PrintEmployee(int userId)
        {
            EmployeeViewModel emp = new EmployeeViewModel();

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "SELECT * FROM Employees WHERE UserId = @UserId";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@UserId", userId);

                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    emp.Id = Convert.ToInt32(dr["Id"]);
                    emp.FullName = dr["FullName"].ToString();
                    emp.Email = dr["Email"].ToString();
                    emp.CompanyName = dr["CompanyName"].ToString();
                    emp.Mobile = dr["Mobile"].ToString();
                    emp.Gender = dr["Gender"].ToString();
                    emp.Address = dr["Address"].ToString();
                    emp.Salary = Convert.ToDecimal(dr["Salary"]);
                    emp.MaritalStatus = dr["MaritalStatus"].ToString();
                }
            }

            return View(emp);
        }
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ForgotPassword(string Email)
        {
            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "SELECT * FROM Users WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Email", Email);

                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    TempData["Email"] = Email;
                    return RedirectToAction("ResetPassword");
                }
                else
                {
                    ViewBag.Error = "Email not found!";
                }
            }

            return View();
        }
        public ActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ResetPassword(string NewPassword)
        {
            string email = TempData["Email"].ToString();

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = "UPDATE Users SET Password = @Password WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@Password", NewPassword);
                cmd.Parameters.AddWithValue("@Email", email);

                con.Open();
                cmd.ExecuteNonQuery();
            }

            ViewBag.Message = "Password updated successfully!";
            return View();
        }
    }
}
