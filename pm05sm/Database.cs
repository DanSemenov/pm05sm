using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace pm05sm;

public sealed record AppUser(long Id, string Login, string FullName, string Role);

public static class Database
{
    private const int MaxFailedAttempts = 3;
    private const int LockMinutes = 3;
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "repairdesk.db");
    private static string ConnectionString => $"Data Source={FilePath};Foreign Keys=True";
    public static SqliteConnection Open() { var connection = new SqliteConnection(ConnectionString); connection.Open(); return connection; }
    public static void Initialize()
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Roles (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE);
            CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Login TEXT NOT NULL UNIQUE, PasswordHash TEXT NOT NULL, PasswordSalt TEXT NOT NULL, FullName TEXT NOT NULL, RoleId INTEGER NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, FOREIGN KEY(RoleId) REFERENCES Roles(Id));
            CREATE TABLE IF NOT EXISTS LoginAttempts (Id INTEGER PRIMARY KEY AUTOINCREMENT, UserLogin TEXT NOT NULL, IsSuccess INTEGER NOT NULL, Message TEXT NOT NULL, CreatedAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Clients (Id INTEGER PRIMARY KEY AUTOINCREMENT, FullName TEXT NOT NULL, Phone TEXT NOT NULL, Email TEXT, Notes TEXT, IsActive INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE IF NOT EXISTS Devices (Id INTEGER PRIMARY KEY AUTOINCREMENT, ClientId INTEGER NOT NULL, DeviceType TEXT NOT NULL, Brand TEXT NOT NULL, Model TEXT NOT NULL, SerialNumber TEXT UNIQUE, Description TEXT, FOREIGN KEY(ClientId) REFERENCES Clients(Id));
            CREATE TABLE IF NOT EXISTS Masters (Id INTEGER PRIMARY KEY AUTOINCREMENT, FullName TEXT NOT NULL, Phone TEXT NOT NULL, Specialization TEXT NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE IF NOT EXISTS Parts (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Article TEXT NOT NULL UNIQUE, Price REAL NOT NULL DEFAULT 0, Quantity INTEGER NOT NULL DEFAULT 0, IsActive INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE IF NOT EXISTS RepairRequests (Id INTEGER PRIMARY KEY AUTOINCREMENT, DeviceId INTEGER NOT NULL, MasterId INTEGER, ReceivedAt TEXT NOT NULL, Problem TEXT NOT NULL, Status TEXT NOT NULL, Priority TEXT NOT NULL DEFAULT 'Обычный', EstimatedCost REAL NOT NULL DEFAULT 0, CompletedAt TEXT, FOREIGN KEY(DeviceId) REFERENCES Devices(Id), FOREIGN KEY(MasterId) REFERENCES Masters(Id));
            """;
        command.ExecuteNonQuery(); Execute(db, "INSERT OR IGNORE INTO Roles (Id, Name) VALUES (1, 'admin'), (2, 'operator'), (3, 'user');");
        if (ScalarLong(db, "SELECT COUNT(*) FROM Users") == 0) { AddUser(db, "admin", "admin123", "Администратор системы", 1); AddUser(db, "operator", "oper123", "Оператор приёмки", 2); AddUser(db, "user", "user123", "Пользователь", 3); }
        if (ScalarLong(db, "SELECT COUNT(*) FROM Clients") == 0)
        {
            Execute(db, "INSERT INTO Clients(FullName,Phone,Email,Notes) VALUES ('Иванов Сергей Петрович','+7 900 123-45-67','ivanov@example.ru','Постоянный клиент'),('Петрова Анна Викторовна','+7 901 555-19-20','petrova@example.ru','');");
            Execute(db, "INSERT INTO Masters(FullName,Phone,Specialization) VALUES ('Кузнецов Андрей Олегович','+7 903 222-10-10','Ноутбуки и ПК'),('Соколова Мария Игоревна','+7 904 777-44-55','Смартфоны и планшеты');");
            Execute(db, "INSERT INTO Devices(ClientId,DeviceType,Brand,Model,SerialNumber,Description) VALUES (1,'Ноутбук','ASUS','VivoBook 15','AS15-2026-001','Не включается'),(2,'Смартфон','Samsung','Galaxy A54','SM-A54-0002','Быстро разряжается');");
            Execute(db, "INSERT INTO Parts(Name,Article,Price,Quantity) VALUES ('SSD Kingston 512 ГБ','SSD-KC512',4200,5),('Аккумулятор Samsung A54','BAT-SA54',2800,3);");
            Execute(db, "INSERT INTO RepairRequests(DeviceId,MasterId,ReceivedAt,Problem,Status,Priority,EstimatedCost) VALUES (1,1,datetime('now'),'Не включается после обновления','В работе','Высокий',6500),(2,2,datetime('now'),'Быстро разряжается аккумулятор','Принята','Обычный',3200);");
        }
    }
    public static bool Register(string login, string fullName, string password, out string error) { error=""; if(login.Trim().Length<3){error="Логин должен содержать не менее 3 символов.";return false;}if(fullName.Trim().Length<3){error="Укажите ФИО пользователя.";return false;}if(password.Length<8){error="Пароль должен содержать не менее 8 символов.";return false;}using var db=Open();if(ScalarLong(db,"SELECT COUNT(*) FROM Users WHERE lower(Login)=lower($login)",("$login",login.Trim()))>0){error="Этот логин уже зарегистрирован.";return false;}AddUser(db,login.Trim(),password,fullName.Trim(),3);return true; }
    public static AppUser? Authenticate(string login, string password, out string message, out bool locked)
    {
        login=login.Trim(); locked=false; message=""; using var db=Open();
        if (IsLocked(db, login)) { locked=true; message=$"Вход временно заблокирован на {LockMinutes} минуты."; return null; }
        AppUser? user=null;
        using (var command=db.CreateCommand())
        {
            command.CommandText="SELECT u.Id,u.Login,u.FullName,r.Name,u.PasswordHash,u.PasswordSalt,u.IsActive FROM Users u JOIN Roles r ON r.Id=u.RoleId WHERE lower(u.Login)=lower($login)";
            command.Parameters.AddWithValue("$login",login);
            using var reader=command.ExecuteReader();
            if(reader.Read() && reader.GetInt64(6)!=0 && PasswordHasher.Verify(password,reader.GetString(4),reader.GetString(5))) user=new AppUser(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetString(3));
        }
        if(user is null) return RegisterFailedAttempt(db,login,"Неверный логин или пароль.",out message,out locked);
        LogAttempt(db,login,true,"Успешный вход"); message="Вход выполнен."; return user;
    }
    public static void RegisterCaptchaFailure(string login, out string message, out bool locked)
    {
        using var db=Open(); login=login.Trim();
        if (IsLocked(db,login)) { locked=true; message=$"Вход временно заблокирован на {LockMinutes} минуты."; return; }
        RegisterFailedAttempt(db,login,"Неверно введена CAPTCHA.",out message,out locked);
    }
    private static AppUser? RegisterFailedAttempt(SqliteConnection db,string login,string reason,out string message,out bool locked)
    {
        LogAttempt(db,login,false,reason); locked=RecentFailures(db,login)>=MaxFailedAttempts;
        message=locked?$"Три неверные попытки. Вход заблокирован на {LockMinutes} минуты.":reason;
        return null;
    }
    private static bool IsLocked(SqliteConnection db,string login)=>RecentFailures(db,login)>=MaxFailedAttempts;
    private static long RecentFailures(SqliteConnection db,string login)=>ScalarLong(db,"SELECT COUNT(*) FROM LoginAttempts WHERE lower(UserLogin)=lower($login) AND IsSuccess=0 AND CreatedAt >= datetime('now',$period)",("$login",login),("$period",$"-{LockMinutes} minutes"));
    public static List<Dictionary<string,object?>> Query(string sql,params (string,object?)[] args){using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText=sql;foreach(var(n,v)in args)cmd.Parameters.AddWithValue(n,v??DBNull.Value);using var reader=cmd.ExecuteReader();var rows=new List<Dictionary<string,object?>>();while(reader.Read()){var row=new Dictionary<string,object?>();for(var i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}return rows;}
    public static int Execute(string sql,params (string,object?)[] args){using var db=Open();return Execute(db,sql,args);} private static int Execute(SqliteConnection db,string sql,params (string,object?)[] args){using var c=db.CreateCommand();c.CommandText=sql;foreach(var(n,v)in args)c.Parameters.AddWithValue(n,v??DBNull.Value);return c.ExecuteNonQuery();} private static long ScalarLong(SqliteConnection db,string sql,params (string,object?)[] args){using var c=db.CreateCommand();c.CommandText=sql;foreach(var(n,v)in args)c.Parameters.AddWithValue(n,v??DBNull.Value);return Convert.ToInt64(c.ExecuteScalar());} private static void LogAttempt(SqliteConnection db,string login,bool ok,string message)=>Execute(db,"INSERT INTO LoginAttempts(UserLogin,IsSuccess,Message,CreatedAt) VALUES($l,$s,$m,datetime('now'))",("$l",login),("$s",ok?1:0),("$m",message)); private static void AddUser(SqliteConnection db,string login,string password,string name,long role){var salt=PasswordHasher.CreateSalt();Execute(db,"INSERT INTO Users(Login,PasswordHash,PasswordSalt,FullName,RoleId,CreatedAt) VALUES($l,$h,$s,$n,$r,datetime('now'))",("$l",login),("$h",PasswordHasher.Hash(password,salt)),("$s",salt),("$n",name),("$r",role));}
}
public static class PasswordHasher{public static string CreateSalt()=>Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));public static string Hash(string password,string salt)=>Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password),Convert.FromBase64String(salt),100_000,HashAlgorithmName.SHA256,32));public static bool Verify(string password,string hash,string salt)=>CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(hash),Convert.FromBase64String(Hash(password,salt)));}
