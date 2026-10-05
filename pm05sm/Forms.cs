using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace pm05sm;

public static class Ui
{
    public static readonly Color Navy = Color.FromArgb(20, 37, 63);
    public static readonly Color Blue = Color.FromArgb(32, 99, 155);
    public static readonly Color Light = Color.FromArgb(244, 247, 251);
    public static Button Button(string text, EventHandler handler, bool primary = true) => new Button { Text = text, AutoSize = true, Padding = new Padding(10, 6, 10, 6), FlatStyle = FlatStyle.Flat, BackColor = primary ? Blue : Color.White, ForeColor = primary ? Color.White : Navy, FlatAppearance = { BorderColor = Blue } }.Also(b => b.Click += handler);
    private static T Also<T>(this T control, Action<T> action) where T : Control { action(control); return control; }
    public static Label Label(string text, int size = 10, bool bold = false) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Navy, Margin = new Padding(0, 6, 0, 3) };
    public static void Style(Form form, string title, Size size) { form.Text = title; form.Size = size; form.MinimumSize = size; form.StartPosition = FormStartPosition.CenterScreen; form.BackColor = Light; form.Font = new Font("Segoe UI", 10); }
}

public sealed class LoginForm : Form
{
    private readonly TextBox _login = new() { PlaceholderText = "Логин", Width = 300 };
    private readonly TextBox _password = new() { PlaceholderText = "Пароль", Width = 300, UseSystemPasswordChar = true };
    private readonly TextBox _captcha = new() { PlaceholderText = "Введите код", Width = 170, Visible = false };
    private readonly Label _captchaCode = new() { AutoSize = true, Visible = false, Font = new Font("Consolas", 16, FontStyle.Bold), BackColor = Color.FromArgb(230, 236, 246), Padding = new Padding(12, 5, 12, 5) };
    private string _code = "";
    public LoginForm()
    {
        Ui.Style(this, "RepairDesk — вход", new Size(500, 520));
        var card = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(45), BackColor = Color.White, Dock = DockStyle.Fill };
        card.Controls.Add(Ui.Label("RepairDesk", 24, true)); card.Controls.Add(Ui.Label("Учёт ремонта компьютерной техники", 11));
        card.Controls.Add(new Label { Height = 15 }); card.Controls.Add(Ui.Label("Вход в систему", 13, true)); card.Controls.Add(_login); card.Controls.Add(_password);
        var cap = new FlowLayoutPanel { AutoSize = true }; cap.Controls.Add(_captchaCode); cap.Controls.Add(_captcha); card.Controls.Add(cap);
        var signIn = Ui.Button("Войти", (_, _) => SignIn()); var register = Ui.Button("Регистрация", (_, _) => new RegisterForm().ShowDialog(this), false);
        var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 14, 0, 0) }; actions.Controls.Add(signIn); actions.Controls.Add(register); card.Controls.Add(actions);
        card.Controls.Add(Ui.Label("Тестовые записи: admin / admin123; operator / oper123; user / user123", 9)); Controls.Add(card); AcceptButton = signIn;
    }
    private void NewCaptcha() { _code = Random.Shared.Next(10000, 99999).ToString(); _captchaCode.Text = _code; _captcha.Visible = _captchaCode.Visible = true; _captcha.Clear(); }
    private void SignIn()
    {
        if (_captcha.Visible && !string.Equals(_captcha.Text.Trim(), _code, StringComparison.Ordinal))
        {
            Database.RegisterCaptchaFailure(_login.Text, out var captchaMessage, out var captchaLocked);
            MessageBox.Show(captchaMessage, "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (!captchaLocked) NewCaptcha();
            return;
        }
        var user = Database.Authenticate(_login.Text, _password.Text, out var message, out var locked);
        if (user is null) { MessageBox.Show(message, "Вход", MessageBoxButtons.OK, MessageBoxIcon.Warning); if (!locked) NewCaptcha(); return; }
        Hide(); using var main = new MainForm(user); main.ShowDialog(); Close();
    }
}

public sealed class RegisterForm : Form
{
    private readonly TextBox _name = new() { Width = 330 }; private readonly TextBox _login = new() { Width = 330 }; private readonly TextBox _password = new() { Width = 330, UseSystemPasswordChar = true }; private readonly TextBox _confirm = new() { Width = 330, UseSystemPasswordChar = true };
    public RegisterForm()
    {
        Ui.Style(this, "Регистрация пользователя", new Size(470, 440)); var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(45), BackColor = Color.White }; panel.Controls.Add(Ui.Label("Создание учётной записи", 18, true));
        Add(panel, "ФИО", _name); Add(panel, "Логин", _login); Add(panel, "Пароль (не менее 8 символов)", _password); Add(panel, "Подтверждение пароля", _confirm); panel.Controls.Add(Ui.Button("Зарегистрироваться", (_, _) => Register())); Controls.Add(panel);
    }
    private static void Add(TableLayoutPanel p, string label, Control control) { p.Controls.Add(Ui.Label(label)); p.Controls.Add(control); }
    private void Register() { if (_password.Text != _confirm.Text) { MessageBox.Show("Пароли не совпадают."); return; } if (Database.Register(_login.Text, _name.Text, _password.Text, out var error)) { MessageBox.Show("Учётная запись создана. Теперь можно войти."); Close(); } else MessageBox.Show(error, "Регистрация", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
}

public sealed class MainForm : Form
{
    private readonly AppUser _user; private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Ui.Light, Padding = new Padding(25) };
    public MainForm(AppUser user)
    {
        _user = user; Ui.Style(this, "RepairDesk", new Size(1180, 720));
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(16, 12, 16, 8), BackColor = Ui.Navy, WrapContents = false };
        nav.Controls.Add(new Label { Text = "RepairDesk", AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White, Margin = new Padding(0, 5, 25, 0) });
        foreach (var item in new[] { ("Заявки", "requests"), ("Устройства", "devices"), ("Клиенты", "clients"), ("Запчасти", "parts"), ("Мастера", "masters") }) nav.Controls.Add(Nav(item.Item1, () => OpenSection(item.Item2)));
        if (_user.Role == "admin") { nav.Controls.Add(Nav("Пользователи", () => OpenSection("users"))); nav.Controls.Add(Nav("Журнал входов", () => OpenSection("logs"))); }
        nav.Controls.Add(Nav("Выход", Close)); Controls.Add(_content); Controls.Add(nav); ShowDashboard();
    }
    private Button Nav(string text, Action action) { var b = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Ui.Navy, ForeColor = Color.White, Padding = new Padding(8, 4, 8, 4), Margin = new Padding(2, 3, 2, 0) }; b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => action(); return b; }
    private void ShowDashboard()
    {
        _content.Controls.Clear(); var greeting = Ui.Label($"Здравствуйте, {_user.FullName}", 22, true); _content.Controls.Add(greeting); var role = Ui.Label($"Роль: {_user.Role}. Рабочее место сервисного центра.", 11); role.Top = 48; _content.Controls.Add(role);
        var grid = new FlowLayoutPanel { Top = 100, Left = 25, Width = 1050, Height = 260, AutoScroll = true }; foreach (var (title, count, text) in new[] { ("Открытые заявки", "SELECT COUNT(*) FROM RepairRequests WHERE Status <> 'Выдана'", "Приём и контроль ремонта"), ("Клиенты", "SELECT COUNT(*) FROM Clients WHERE IsActive=1", "Контакты заказчиков"), ("Запчасти", "SELECT COUNT(*) FROM Parts WHERE Quantity>0", "Доступные позиции"), ("Мастера", "SELECT COUNT(*) FROM Masters WHERE IsActive=1", "Исполнители работ") }) { var card = new Panel { Size = new Size(230, 150), BackColor = Color.White, Margin = new Padding(0, 0, 18, 18), Padding = new Padding(18) }; card.Controls.Add(Ui.Label(title, 11, true)); var val=Database.Query(count)[0].Values.First()?.ToString() ?? "0"; var number=Ui.Label(val,28,true); number.Top=44; card.Controls.Add(number); var desc=Ui.Label(text,9); desc.Top=105; card.Controls.Add(desc);grid.Controls.Add(card); } _content.Controls.Add(grid);
        var note = Ui.Label("Модификация варианта: в разделе заявок реализован фильтр по периоду и статусу. Это позволяет контролировать поток работ в сервисном центре.", 11); note.Top=390;note.MaximumSize=new Size(950,0);_content.Controls.Add(note);
    }
    private void OpenSection(string section) { _content.Controls.Clear(); _content.Controls.Add(new CrudView(section, _user.Role != "user")); }
}

public sealed class CrudView : UserControl
{
    private readonly string _section; private readonly bool _canEdit; private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false }; private readonly TextBox _search = new() { Width = 210, PlaceholderText = "Поиск" }; private ComboBox? _status; private DateTimePicker? _from; private DateTimePicker? _to;
    private static readonly Dictionary<string,(string title,string table,string[] fields,string display,string search)> Specs = new()
    {
        ["clients"]=("Клиенты","Clients",new[]{"FullName","Phone","Email","Notes"},"Id, FullName AS 'ФИО', Phone AS 'Телефон', Email AS 'E-mail', Notes AS 'Примечание'","FullName || ' ' || Phone || ' ' || ifnull(Email,'')"),
        ["devices"]=("Устройства","Devices",new[]{"ClientId","DeviceType","Brand","Model","SerialNumber","Description"},"d.Id, c.FullName AS 'Клиент', d.DeviceType AS 'Тип', d.Brand AS 'Бренд', d.Model AS 'Модель', d.SerialNumber AS 'Серийный номер', d.Description AS 'Описание'","d.DeviceType || ' ' || d.Brand || ' ' || d.Model || ' ' || ifnull(d.SerialNumber,'')"),
        ["masters"]=("Мастера","Masters",new[]{"FullName","Phone","Specialization"},"Id, FullName AS 'ФИО', Phone AS 'Телефон', Specialization AS 'Специализация'","FullName || ' ' || Phone || ' ' || Specialization"),
        ["parts"]=("Запчасти","Parts",new[]{"Name","Article","Price","Quantity"},"Id, Name AS 'Наименование', Article AS 'Артикул', Price AS 'Цена', Quantity AS 'Остаток'","Name || ' ' || Article"),
        ["requests"]=("Заявки на ремонт","RepairRequests",new[]{"DeviceId","MasterId","Problem","Status","Priority","EstimatedCost"},"r.Id, c.FullName AS 'Клиент', d.Brand || ' ' || d.Model AS 'Устройство', m.FullName AS 'Мастер', r.ReceivedAt AS 'Принята', r.Problem AS 'Неисправность', r.Status AS 'Статус', r.Priority AS 'Приоритет', r.EstimatedCost AS 'Стоимость'","r.Problem || ' ' || r.Status || ' ' || r.Priority"),
        ["users"]=("Пользователи","Users",Array.Empty<string>(),"u.Id, u.Login AS 'Логин', u.FullName AS 'ФИО', r.Name AS 'Роль', u.IsActive AS 'Активен', u.CreatedAt AS 'Создан'","u.Login || ' ' || u.FullName"),
        ["logs"]=("Журнал входов","LoginAttempts",Array.Empty<string>(),"Id, UserLogin AS 'Логин', IsSuccess AS 'Успешно', Message AS 'Сообщение', CreatedAt AS 'Дата и время'","UserLogin || ' ' || Message")
    };
    public CrudView(string section,bool canEdit)
    {
        _section=section;_canEdit=canEdit;Dock=DockStyle.Fill;var spec=Specs[section];var head=new FlowLayoutPanel{Dock=DockStyle.Top,Height=92,Padding=new Padding(0,0,0,10),AutoSize=false};head.Controls.Add(Ui.Label(spec.title,18,true));head.SetFlowBreak(head.Controls[0],true);head.Controls.Add(_search);head.Controls.Add(Ui.Button("Найти",(_,_)=>LoadData(),false));
        if(section=="requests"){_status=new ComboBox{Width=120,DropDownStyle=ComboBoxStyle.DropDownList};_status.Items.AddRange(new object[]{"Все статусы","Принята","В работе","Ожидание деталей","Готова","Выдана"});_status.SelectedIndex=0;_from=new DateTimePicker{Width=120,Value=DateTime.Today.AddDays(-30)};_to=new DateTimePicker{Width=120,Value=DateTime.Today.AddDays(1)};head.Controls.Add(Ui.Label("Статус:"));head.Controls.Add(_status);head.Controls.Add(Ui.Label("Период:"));head.Controls.Add(_from);head.Controls.Add(_to);head.Controls.Add(Ui.Button("Фильтр",(_,_)=>LoadData(),false));}
        if(_canEdit&&spec.fields.Length>0){head.Controls.Add(Ui.Button("Добавить",(_,_)=>Edit()));head.Controls.Add(Ui.Button("Изменить",(_,_)=>Edit(SelectedId())));head.Controls.Add(Ui.Button("Удалить",(_,_)=>Delete()));}Controls.Add(_grid);Controls.Add(head);LoadData();
    }
    private long? SelectedId()=>_grid.CurrentRow?.Cells[0].Value is null?null:Convert.ToInt64(_grid.CurrentRow.Cells[0].Value);
    private void LoadData()
    {
        var s=Specs[_section];string sql;var args=new List<(string,object?)>();var where=$" WHERE ({s.search}) LIKE $q";args.Add(("$q","%"+_search.Text.Trim()+"%"));
        if(_section=="devices")sql=$"SELECT {s.display} FROM Devices d JOIN Clients c ON c.Id=d.ClientId{where}";else if(_section=="requests"){if(_status!.SelectedIndex>0){where+=" AND r.Status=$status";args.Add(("$status",_status.Text));}where+=" AND date(r.ReceivedAt) BETWEEN date($from) AND date($to)";args.Add(("$from",_from!.Value.ToString("yyyy-MM-dd")));args.Add(("$to",_to!.Value.ToString("yyyy-MM-dd")));sql=$"SELECT {s.display} FROM RepairRequests r JOIN Devices d ON d.Id=r.DeviceId JOIN Clients c ON c.Id=d.ClientId LEFT JOIN Masters m ON m.Id=r.MasterId{where} ORDER BY r.Id DESC";}else if(_section=="users")sql=$"SELECT {s.display} FROM Users u JOIN Roles r ON r.Id=u.RoleId{where}";else sql=$"SELECT {s.display} FROM {s.table}{where}";
        var rows=Database.Query(sql,args.ToArray());var table=new DataTable();foreach(var row in rows){foreach(var key in row.Keys)if(!table.Columns.Contains(key))table.Columns.Add(key);var dr=table.NewRow();foreach(var p in row)dr[p.Key]=p.Value??DBNull.Value;table.Rows.Add(dr);}_grid.DataSource=table;
    }
    private void Edit(long? id=null){if(id is null&&_grid.Focused&&_section is "users" or "logs")return;using var form=new EntityEditForm(_section,id);if(form.ShowDialog()==DialogResult.OK)LoadData();}
    private void Delete(){var id=SelectedId();if(id is null)return;if(MessageBox.Show("Удалить выбранную запись?","Подтверждение",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;Database.Execute($"DELETE FROM {Specs[_section].table} WHERE Id=$id",("$id",id));LoadData();}
}

public sealed class EntityEditForm : Form
{
    private readonly string _section; private readonly long? _id; private readonly Dictionary<string,Control> _fields=new();
    private static readonly HashSet<string> OptionalFields=new(){"Email","Notes","SerialNumber","Description","MasterId"};
    private static readonly HashSet<string> IntegerFields=new(){"ClientId","DeviceId","MasterId","Quantity"};
    private static readonly HashSet<string> MoneyFields=new(){"Price","EstimatedCost"};
    public EntityEditForm(string section,long? id)
    {
        _section=section; _id=id; Ui.Style(this,id is null?"Добавление записи":"Редактирование записи",new Size(520,560));
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(32),BackColor=Color.White};
        foreach(var field in CrudViewSpecs()) { panel.Controls.Add(Ui.Label(LabelFor(field))); var editor=CreateEditor(field); _fields[field]=editor; panel.Controls.Add(editor); }
        panel.Controls.Add(Ui.Button("Сохранить",(_,_)=>Save())); Controls.Add(panel); if(id is not null)LoadExisting();
    }
    private string[] CrudViewSpecs()=>_section switch{"clients"=>new[]{"FullName","Phone","Email","Notes"},"devices"=>new[]{"ClientId","DeviceType","Brand","Model","SerialNumber","Description"},"masters"=>new[]{"FullName","Phone","Specialization"},"parts"=>new[]{"Name","Article","Price","Quantity"},"requests"=>new[]{"DeviceId","MasterId","Problem","Status","Priority","EstimatedCost"},_=>Array.Empty<string>()};
    private static string LabelFor(string field)=>field switch{"FullName"=>"ФИО (только буквы)","Phone"=>"Телефон (цифры, +, пробел, скобки, дефис)","ClientId"=>"ID клиента (только цифры)","DeviceId"=>"ID устройства (только цифры)","MasterId"=>"ID мастера (только цифры, можно оставить пустым)","DeviceType"=>"Тип устройства","SerialNumber"=>"Серийный номер","Description"=>"Описание","Specialization"=>"Специализация","Name"=>"Наименование","Article"=>"Артикул","Price"=>"Цена (число)","Quantity"=>"Количество (целое число)","Problem"=>"Неисправность","Status"=>"Статус","Priority"=>"Приоритет","EstimatedCost"=>"Оценочная стоимость (число)",_=>field};
    private Control CreateEditor(string field)
    {
        if(field=="Status") return new ComboBox{Width=400,DropDownStyle=ComboBoxStyle.DropDownList,DataSource=new[]{"Принята","В работе","Ожидание деталей","Готова","Выдана"}};
        if(field=="Priority") return new ComboBox{Width=400,DropDownStyle=ComboBoxStyle.DropDownList,DataSource=new[]{"Обычный","Высокий","Срочный"}};
        var box=new TextBox{Width=400,Multiline=field is "Description" or "Problem",Height=field is "Description" or "Problem"?60:28};
        ApplyInputRule(field,box); return box;
    }
    private static void ApplyInputRule(string field,TextBox box)
    {
        box.KeyPress += (_,e) =>
        {
            if(char.IsControl(e.KeyChar)) return;
            if(IntegerFields.Contains(field)) e.Handled=!char.IsDigit(e.KeyChar);
            else if(MoneyFields.Contains(field)) e.Handled=!(char.IsDigit(e.KeyChar) || ((e.KeyChar==',' || e.KeyChar=='.') && !box.Text.Contains(',') && !box.Text.Contains('.')));
            else if(field=="Phone") e.Handled=!(char.IsDigit(e.KeyChar) || "+ -()".Contains(e.KeyChar));
            else if(field=="FullName") e.Handled=!(char.IsLetter(e.KeyChar) || char.IsWhiteSpace(e.KeyChar) || "-'".Contains(e.KeyChar));
        };
    }
    private string TextOf(string field)=>_fields[field] switch { TextBox box=>box.Text.Trim(), ComboBox list=>list.Text.Trim(), _=>"" };
    private void SetText(string field,string value)
    {
        if(_fields[field] is TextBox box) box.Text=value;
        else if(_fields[field] is ComboBox list) list.SelectedItem=value;
    }
    private void LoadExisting(){var row=Database.Query($"SELECT * FROM {(_section=="requests"?"RepairRequests":_section[..1].ToUpper()+_section[1..])} WHERE Id=$id",("$id",_id))[0];foreach(var field in _fields.Keys)if(row.TryGetValue(field,out var value)&&value is not null)SetText(field,value.ToString()!);}
    private bool IsValid(out string error)
    {
        foreach(var field in _fields.Keys) if(!OptionalFields.Contains(field) && string.IsNullOrWhiteSpace(TextOf(field))) { error="Заполните поле: "+LabelFor(field); return false; }
        foreach(var field in IntegerFields.Where(_fields.ContainsKey)) { var text=TextOf(field); if(string.IsNullOrWhiteSpace(text) && field=="MasterId") continue; if(!long.TryParse(text,out var number) || number<=0) { error=LabelFor(field)+" должен содержать положительное целое число."; return false; } }
        foreach(var field in MoneyFields.Where(_fields.ContainsKey)) if(!double.TryParse(TextOf(field).Replace(',', '.'),NumberStyles.Number,CultureInfo.InvariantCulture,out var value) || value<0) { error=LabelFor(field)+" должна быть неотрицательным числом."; return false; }
        if(_section is "clients" or "masters")
        {
            var phone=TextOf("Phone");
            if(!Regex.IsMatch(phone,@"^[+0-9 ()-]{10,24}$") || phone.Count(char.IsDigit)<10) { error="Укажите корректный номер телефона."; return false; }
        }
        if(_section=="clients" && !string.IsNullOrWhiteSpace(TextOf("Email")) && !Regex.IsMatch(TextOf("Email"),@"^[^@\s]+@[^@\s]+\.[^@\s]+$")) { error="Укажите корректный e-mail."; return false; }
        if(_fields.ContainsKey("FullName") && !Regex.IsMatch(TextOf("FullName"),@"^[\p{L}\s'-]{3,}$")) { error="ФИО может содержать только буквы, пробелы, дефис и апостроф."; return false; }
        error=""; return true;
    }
    private void Save(){try{if(!IsValid(out var error)){MessageBox.Show(error,"Проверка данных",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}var table=_section switch{"clients"=>"Clients","devices"=>"Devices","masters"=>"Masters","parts"=>"Parts","requests"=>"RepairRequests",_=>""};var columns=string.Join(",",_fields.Keys);var parameters=_fields.Keys.Select(x=>"$"+x);if(_id is null){var extra=_section=="requests"?",ReceivedAt": "";Database.Execute($"INSERT INTO {table}({columns}{extra}) VALUES({string.Join(",",parameters)}{(_section=="requests"?",datetime('now')":"")})",Values().ToArray());}else{Database.Execute($"UPDATE {table} SET {string.Join(",",_fields.Keys.Select(x=>$"{x}=${x}"))} WHERE Id=$id",Values().Append(("$id",(object?)_id)).ToArray());}DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show("Не удалось сохранить запись: "+ex.Message,"Ошибка",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    private IEnumerable<(string,object?)> Values()=>_fields.Keys.Select(field=>("$"+field,ValueOf(field)));
    private object? ValueOf(string field)
    {
        var text=TextOf(field); if(field=="MasterId" && string.IsNullOrWhiteSpace(text)) return null;
        if(IntegerFields.Contains(field)) return long.Parse(text);
        if(MoneyFields.Contains(field)) return double.Parse(text.Replace(',', '.'),CultureInfo.InvariantCulture);
        return text;
    }
}
