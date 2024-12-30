using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpreadCheetah;
using SpreadCheetah.Worksheets;
using SpreadCheetah.Styling;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Path = System.IO.Path;
using Style = SpreadCheetah.Styling.Style;

namespace LogTagger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DataTable tag_data = new DataTable();
        DataTable log_data = new DataTable();

        public MainWindow()
        {
            InitializeComponent();

            tag_data.Columns.Add("단어", typeof(string));
            tag_data.Columns.Add("태그", typeof(string));


            log_data.Columns.Add("일시", typeof(string));
            log_data.Columns.Add("보낸이", typeof(string));
            log_data.Columns.Add("내용", typeof(string));
            log_data.Columns.Add("태그", typeof(string));

            Load_Data();
        }

        private void ShowMessageBox(string message)
        {
            string caption = "알림";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.None;

            MessageBox.Show(message, caption, button, icon);
        }

        private void Button_Log_Open_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".txt",
                Title = "채팅 파일 열기",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            Nullable<bool> result = dlg.ShowDialog();

            if (result != true) return;

            string filename = dlg.FileName;
            using (StreamReader sr = File.OpenText(filename))
            {
                string line;
                string lineHeader = "";
                string linePost = "";
                string currentDay = "";
                int cnt = 0;
                bool firstFlag = true;

                Regex chatFirstRegex = new Regex(@"^\[.*\] \[.*[0-9]+:[0-9]+\]");
                Regex chatDayRegex = new Regex(@"^-+.+-+$");
                Regex chatInsideBracket = new Regex(@"\[([^\]]+)\]");

                while ((line = sr.ReadLine()) != null)
                {
                    cnt++;
                    if (cnt < 3)
                    {
                        continue;
                    }
                    string linet = line + "\n";

                    void addRow()
                    {
                        if (!linePost.Contains(Company_Text.Text)) return;

                        MatchCollection matches = chatInsideBracket.Matches(lineHeader);
                        string time = currentDay + " " + matches[1].Groups[1].Value;
                        string name = matches[0].Groups[1].Value;
                        string tag = "";
                        List<string> tags = new List<string>();
                        foreach (DataRowView row in tag_Table.ItemsSource)
                        {
                            if (linePost.Contains(row[0].ToString()))
                            {
                                tags.Add(row[1].ToString());
                            }
                        }
                        tag = string.Join(", ", tags);
                        log_data.Rows.Add(new string[] { time, name, linePost, tag });
                    }

                    if (chatDayRegex.IsMatch(linet))
                    {
                        if (!firstFlag)
                        {
                            addRow();
                        }
                        firstFlag = true;

                        currentDay = chatDayRegex.Match(linet).ToString().Replace("-", "").Trim();
                    } 
                    else if (chatFirstRegex.IsMatch(linet))
                    {
                        if (!firstFlag)
                        {
                            addRow();
                        }
                        firstFlag = false;

                        lineHeader = chatFirstRegex.Match(linet).ToString().Trim();
                        linePost = chatFirstRegex.Replace(linet, "");
                    } 
                    else
                    {
                        linePost += linet;
                    }
                }
                log_Table.ItemsSource = log_data.DefaultView;
                log_Table.IsReadOnly = true;
                log_Table.SelectionMode = DataGridSelectionMode.Single;
                log_Table.Columns[0].Width = DataGridLength.SizeToCells;
                log_Table.Columns[2].MaxWidth = 500;
            }
        }

        private void Load_Data()
        {
            string filename = Path.Join(Environment.CurrentDirectory, "data.json");
            if (!File.Exists(filename))
            {
                tag_Table.ItemsSource = tag_data.DefaultView;
                tag_Table.IsReadOnly = false;
                tag_Table.SelectionMode = DataGridSelectionMode.Single;
                return;
            }
            using (StreamReader jsonData = File.OpenText(filename))
            using (JsonTextReader reader = new JsonTextReader(jsonData))
            {
                JObject jsonO = (JObject)JToken.ReadFrom(reader);
                Company_Text.Text = (string)jsonO["company_name"];

                tag_data.Rows.Clear();
                JArray jArray = (JArray)jsonO["tags"];
                foreach (JObject jObject in jArray)
                {
                    tag_data.Rows.Add(new string[] { (string)jObject["word"], (string)jObject["tag"] });
                }

                tag_Table.ItemsSource = tag_data.DefaultView;
                tag_Table.IsReadOnly = false;
                tag_Table.SelectionMode = DataGridSelectionMode.Single;
            }
        }

        private void Save_Data()
        {
            string filename = Path.Join(Environment.CurrentDirectory, "data.json");
            var saveJson = new JObject();
            saveJson.Add("company_name", Company_Text.Text);
            var tags = new JArray();
            foreach (DataRowView row in tag_Table.ItemsSource)
            {
                var jo = new JObject
                {
                    { "word", row[0].ToString() },
                    { "tag", row[1].ToString() }
                };
                tags.Add(jo);
            }
            saveJson.Add("tags", tags);
            File.WriteAllText(filename, saveJson.ToString());

            ShowMessageBox("태그 저장이 완료되었습니다.");
        }

        private void Button_Add_Tag_Click(object sender, RoutedEventArgs e)
        {
            tag_data.Rows.Add();
        }

        private void Button_Delete_Tag_Click(object sender, RoutedEventArgs e)
        {
            if (tag_data.Rows.Count <= tag_Table.SelectedIndex) return;
            tag_data.Rows.RemoveAt(tag_Table.SelectedIndex);
        }

        private void Button_Save_Tag_Click(object sender, RoutedEventArgs e)
        {
            Save_Data();
        }

        private async void Button_Log_Export_Click(object sender, RoutedEventArgs e)
        {
            string filename = Path.Join(Environment.CurrentDirectory, $"{DateTime.Now.ToLongDateString()}_result.xlsx");

            using (var stream = File.Create(filename))
            using (var spreadsheet = await Spreadsheet.CreateNewAsync(stream))
            {
                var worksheetOptions = new WorksheetOptions();
                worksheetOptions.Column(1).Width = 40;
                worksheetOptions.Column(2).Width = 30;
                worksheetOptions.Column(3).Width = 100;
                await spreadsheet.StartWorksheetAsync("Sheet 1", worksheetOptions);

                var row = new List<Cell>();
                for (int i = 0; i < log_data.Columns.Count; i++)
                {
                    row.Add(new Cell(log_data.Columns[i].ColumnName));
                }
                await spreadsheet.AddRowAsync(row);

                var postStyle = new Style();
                postStyle.Alignment.WrapText = true;
                postStyle.Alignment.Vertical = SpreadCheetah.Styling.VerticalAlignment.Top;
                var postStyleId = spreadsheet.AddStyle(postStyle);
                for (int i = 0; i < log_data.Rows.Count; i++)
                {
                    var logRow = new List<Cell>();
                    for (int j = 0; j < log_data.Columns.Count; j++)
                    {
                        if (j == 1) continue;
                        logRow.Add(new Cell((string?)log_data.Rows[i][j], postStyleId));
                    }
                    var rowOptions = new RowOptions { Height = 50 };

                    await spreadsheet.AddRowAsync(logRow, rowOptions);   
                }

                await spreadsheet.FinishAsync();
            }

            ShowMessageBox("엑셀 파일 저장이 완료되었습니다.");
        }
    }
}