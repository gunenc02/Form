using Bahadır_webForm.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.SqlServer;
using System.Linq;

namespace Bahadır_webForm.Controllers
{
    public class FormController : Controller
    {
        string ConString = "Server=DESKTOP-SHJJUOD; Database=trial; Trusted_Connection=True; MultipleActiveResultSets=true";
        // key ve iv değerleri şifreleme için gereklidir
        private readonly string Key = "This_key_is_used";
        private readonly byte[] iv = {26,55,14,98,45,155,255,197,63,244,218,44,72,13,74,183};
        public IActionResult Form()
        {
            return View();
        }

        // Bu metod "index" sayfasından gelen verileri işlemekte ve veritabanına kaydetmektedir
        [HttpPost]
        public IActionResult CreateEntry(Information user)
        {
            TempData["Message"] = null;
            try
            {
                SqlConnection conn = new SqlConnection(ConString);
                conn.Open();
                if(ShouldBeAdd(conn, user, true))
                {
                    string cmdString = "INSERT INTO dbo.KAYIT_TABLOSU(ISIM, SOY_ISIM, TELEFON_NO, TC_KIMLIK_NO, DOGUM_TARIHI, KAYIT_TARIHI) " +
                    "VALUES (@name, @surname, @phoneNumber, @ssn, @birthday, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(cmdString, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", Encrypt(user.Name));
                        cmd.Parameters.AddWithValue("@surname", Encrypt(user.Surname));
                        cmd.Parameters.AddWithValue("@phoneNumber", Encrypt(user.PhoneNumber));
                        cmd.Parameters.AddWithValue("@ssn", Encrypt(user.Ssn));
                        cmd.Parameters.AddWithValue("@birthday", user.Date);
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();
                        conn.Close();
                        TempData["Message"] = "1Kayıt işlemi başarılı";
                    }
                }               
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
                        
            return RedirectToAction("Index","Home");
        }
        // bu metod girilen değerlerin veritabanına kaydedilip kaydedilmemesine gerektiğine karar vermekte
        private bool ShouldBeAdd(SqlConnection conn, Information info, bool newUser)
        {
            var reg = new Regex(@"\(\d{3}\) \d{3} \d{4}");
            try
            {
                if(info.Ssn == null || info.Ssn.Length != 11)
                {
                    TempData["Message"] = "0TC kimlik numarası 11 haneli olmak zorundadır";
                    conn.Close();
                    return false;
                }
                string check = "SELECT COUNT (*) FROM dbo.KAYIT_TABLOSU WHERE TC_KIMLIK_NO = @ssn;";

                using (SqlCommand cmd = new SqlCommand(check, conn))
                {
                    cmd.Parameters.AddWithValue("@ssn", info.Ssn);
                    int recordNum = (int)cmd.ExecuteScalar();
                    if (recordNum > 0 && newUser)
                    {
                        TempData["Message"] = "0Kullanıcı kaydı mevcut";
                        cmd.Dispose();
                        conn.Close();
                        return false;
                    }
                    else if (info.Date > DateTime.Now)
                    {
                        TempData["Message"] = "0Kayıtta tutarsızlık";
                        cmd.Dispose();
                        conn.Close();
                        return false;
                    }
                    else if (info.PhoneNumber == null || info.PhoneNumber.Length != 14 || !(reg.IsMatch(info.PhoneNumber)) || info.PhoneNumber[1] != '5')
                    {
                        TempData["Message"] = "0Telefon numarası belirtilen formatla uyuşmamaktadır";
                        cmd.Dispose();
                        conn.Close();
                        return false;
                    }
                    else
                    {
                        cmd.Dispose();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        // Bu metod sadece adminlerin giriş yapabileceği ve kaydolan kullanıcı bilgilerini görebilecekleri ekran 
        [Authorize(Roles = "ADMIN")]
        public IActionResult ShowTable(bool IsMessageDisplayed)
        {
            ViewBag.Status = IsMessageDisplayed;
            DataTable table = new DataTable();
            using (SqlConnection conn = new SqlConnection(ConString))
            {
                conn.Open();
                SqlDataAdapter adapter = new SqlDataAdapter("SELECT  * FROM KAYIT_TABLOSU", conn);
                adapter.Fill(table);

                for(int i = 0; i < table.Rows.Count; i++)
                {
                    for(int j = 1; j < 5; j++)
                    {
                        table.Rows[i][j] = Decrypt(table.Rows[i][j].ToString());
                    }
                }

                adapter.Dispose();
                conn.Close();
            }            
            return View(table);
        }

        //Bu ekran adminlerin kayıtlı kişilerin bilgileri üzerinde oynama yapabilecekleri arayüzü sağlar
        [HttpGet]
        [Authorize(Roles = "ADMIN")]
        public IActionResult Edit(int id)
        {
            Information info = new Information();
            DataTable table = new DataTable();
            using(SqlConnection conn = new SqlConnection(ConString))
            {
                conn.Open();
                string query = "SELECT *  FROM dbo.KAYIT_TABLOSU WHERE ID=@id";
                SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                adapter.SelectCommand.Parameters.AddWithValue("@id", id);
                adapter.Fill(table);
                conn.Close();
            }
            if(table.Rows.Count == 1)
            {
                info.Id = Convert.ToInt32(table.Rows[0][0].ToString());
                info.Name = Decrypt((string)table.Rows[0][1]);
                info.Surname = Decrypt((string)table.Rows[0][2]);
                info.PhoneNumber = Decrypt((string)table.Rows[0][3]);
                info.Ssn = Decrypt((string)table.Rows[0][4]);
                info.Date = Convert.ToDateTime(table.Rows[0][5]);
                info.EntryDate = Convert.ToDateTime(table.Rows[0][6]);
                return View(info);
            }
            return RedirectToAction("ShowTable", "Form");
        }

        // Bu metod adminlerin kullanıcı bilgilerini üzerinde yaptığı değişiliklerin veritabanına kaydedilmesine yarar
        [HttpPost]
        public IActionResult Edit(Information info)
        {
            TempData["Message"] = null;

            try
            {              
                using (SqlConnection conn = new SqlConnection(ConString))
                {
                    conn.Open();
                    if (ShouldBeAdd(conn, info, false))
                    {
                        string query = "UPDATE dbo.KAYIT_TABLOSU SET ISIM=@isim, SOY_ISIM=@surname, TELEFON_NO = @phoneNumber, TC_KIMLIK_NO=@ssn, DOGUM_TARIHI=@birthday, KAYIT_TARIHI= GETDATE() WHERE ID=@id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", info.Id);
                            cmd.Parameters.AddWithValue("@isim", Encrypt(info.Name));
                            cmd.Parameters.AddWithValue("@surname", Encrypt(info.Surname));
                            cmd.Parameters.AddWithValue("@phoneNumber", Encrypt(info.PhoneNumber));
                            cmd.Parameters.AddWithValue("@ssn", Encrypt(info.Ssn));
                            cmd.Parameters.AddWithValue("@birthday", info.Date);
                            cmd.ExecuteNonQuery();
                            cmd.Dispose();
                        }
                           
                        conn.Close();
                        TempData["Message"] = "1Başarılı şekilde güncellendi";
                    }
                    else
                    {
                        conn.Close();
                        TempData["Message"] = "0Veriler uygun değil";
                    }
                }
            }
            catch(Exception ex)
            {
                TempData["Message"] = "0Hata";
                Console.WriteLine(ex.Message);
                
            }
            return RedirectToAction("ShowTable", "Form", new { IsMessageDisplayed = false }) ;
        }
        
        //Bu metod adminlerin kayıt silmesini sağlar
        public IActionResult Delete(int id)
        {
            TempData["Message"] = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(ConString))
                {
                    conn.Open();
                    string query = "DELETE FROM dbo.KAYIT_TABLOSU WHERE ID=@id;";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();
                        TempData["Message"] = "1Silme işlemi başarılı";
                    }
                    conn.Close();
                }
            }
            catch(Exception ex)
            {
                TempData["Message"] = "0Hata";
                Console.WriteLine(ex.Message);
            }
            return RedirectToAction("ShowTable", "Form");
        }

        // Bu metod kullanıcıların kendi bilgilerini sorgulamalarını sağlar
        [HttpGet]
        public IActionResult Inquiry()
        {
            return View();
        }

        [HttpPost]
        public JsonResult Inquiry(string info)
        {
            int? id = null;
            if((Convert.ToInt64(info) < 10000000000) || info == null)
            {
                return new JsonResult(-2);
            }
            try
            {
                using (SqlConnection conn = new SqlConnection(ConString))
                {
                    conn.Open();
                    string query = "SELECT ID FROM dbo.KAYIT_TABLOSU WHERE TC_KIMLIK_NO = @ssn";
                    using(SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ssn", Encrypt(info));
                        SqlDataReader reader = cmd.ExecuteReader();
                        while(reader.Read())
                        {
                            id = Convert.ToInt32(reader["ID"]);
                        }
                        cmd.Dispose();
                    }
                    conn.Close();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new JsonResult(0);
            }

            if (id == null)
            {
                return new JsonResult(-1);
            }
            return new JsonResult(id);
        }

        // bu metod kullanıcıların bilgilerini kullanıcılara sunar
        [HttpGet]
        public IActionResult Result(int id)
        {
            Information info = new Information();
            try
            {
                using (SqlConnection conn = new SqlConnection(ConString))
                {
                    conn.Open();
                    string query = "SELECT * FROM dbo.KAYIT_TABLOSU WHERE ID = @id";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        SqlDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            info.Id = Convert.ToInt32(reader["ID"]);
                            info.Name = Decrypt(Convert.ToString(reader["ISIM"]));
                            info.Surname = Decrypt(Convert.ToString(reader["SOY_ISIM"]));
                            info.PhoneNumber = Decrypt(Convert.ToString(reader["TELEFON_NO"]));
                            info.Ssn = Decrypt(Convert.ToString(reader["TC_KIMLIK_NO"]));
                            info.Date = Convert.ToDateTime(reader["DOGUM_TARIHI"]);
                            info.EntryDate = Convert.ToDateTime(reader["KAYIT_TARIHI"]);
                        }
                        cmd.Dispose();
                    }
                    conn.Close();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return RedirectToAction("Inquiry");
            }
            
            return View(info);
        }

        // Bu metod kullanıcıların giriş işlemleri için gerekli arayüzü sağlar
        [HttpGet]
        public IActionResult Login(bool IsMessageDisplayed)
        {
            ViewBag.Status = IsMessageDisplayed;
            return View();
        }

        // Bu metod kullancıların kaydolma işlemlerini gerçekleştirmelerini sağlayıp eğer kaydolma işlemi başarılıysa onlara çerez atar
        [HttpPost]
        public async Task <IActionResult> Login(Admin admin)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConString))
                {
                    conn.Open();
                    string query = "SELECT COUNT(*) FROM dbo.ADMIN_TABLOSU WHERE KULLANICI_ADI=@name AND SIFRE=@password";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", Encrypt(admin.Name));
                        cmd.Parameters.AddWithValue("@password", Encrypt(admin.Password));
                        int recordNum = (int)cmd.ExecuteScalar();
                        if (recordNum == 1)
                        {
                            var claims = new List<Claim>
                            {
                                new Claim(ClaimTypes.Name, admin.Name),
                                new Claim(ClaimTypes.Role, "ADMIN")
                            };

                            ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                            AuthenticationProperties authenticationProperties = new AuthenticationProperties();

                            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authenticationProperties);

                            cmd.Dispose();
                            conn.Close();
                            return RedirectToAction("ShowTable", "Form", new { IsMessageDisplayed = true });
                        }
                        else
                        {
                            cmd.Dispose();
                            conn.Close();
                            return RedirectToAction("Login", "Form");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return RedirectToAction("Login", "Form");
            }

        }

        public async Task <IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Form");
        }

        // bu metod adminlerin şifre değiştirebilmesi için gereken arayüzü sağlar
        [HttpGet]
        public IActionResult ChangePassword(bool IsMessageDisplayed)
        {
            ViewBag.Status = IsMessageDisplayed;
            return View();
        }

        // bu metod get metodundan gelen bilgileri işler
        [HttpPost]
        public IActionResult ChangePassword(Admin admin)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConString))
                {
                    conn.Open();
                    if (IsValueValid(conn, admin))
                    {
                        string query = "UPDATE dbo.ADMIN_TABLOSU SET SIFRE=@password WHERE KULLANICI_ADI=@name";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@name", admin.Name);
                            cmd.Parameters.AddWithValue("@password", admin.Password);
                            cmd.ExecuteNonQuery();
                            cmd.Dispose();
                        }
                        conn.Close();
                        return RedirectToAction("Login", "Form", new {isMessageDisplayed = true});
                    }
                    else
                    {
                        conn.Close();
                    }
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return RedirectToAction("ChangePassword", "Form" , new {isMessageDisplayed = true });
            }
            return RedirectToAction("ChangePassword", "Form", new {isMessageDisplayed = true});
        }

        // Bu metod şifre değiştirme işlemindeki denetlenmesi gereken dueumları denetler
        private bool IsValueValid(SqlConnection conn, Admin admin)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM dbo.ADMIN_TABLOSU WHERE KULLANICI_ADI=@name";
                using(SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@name", admin.Name);
                    int recordNum = (int)cmd.ExecuteScalar();
                    if ((recordNum == 1) && (admin.Compare == admin.Password))
                    {
                        cmd.Dispose();
                        return true;
                    }
                    else if(admin.Compare != admin.Password)
                    {
                        cmd.Dispose();
                        return false;
                    }
                    else {
                        cmd.Dispose();
                        return false;
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        //bu metod veritabanına kaydedilecek verilerin şifrelenmesi için kullanılır
        private string Encrypt(string plainText)
        {
            Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(Key);

            byte[] encryptedBytes;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, iv);

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using(StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                }
                    encryptedBytes = msEncrypt.ToArray();
            }

            return Convert.ToBase64String(encryptedBytes);
        }
        // bu metod veritabanında şifreli bir şekilde duran verilerin kullanıcılara düzgün bir şekilde yansıtılması için kullanılır
        private string Decrypt(string cipherText)
        {
            Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(Key);

            byte[] encryptedData = Convert.FromBase64String(cipherText);

            aes.IV = iv;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using MemoryStream msDecrypt = new MemoryStream(encryptedData);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt);

            return srDecrypt.ReadToEnd();
        }
    }
}
