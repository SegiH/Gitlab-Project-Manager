using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

// see if separate thread is needed

namespace GMSGitlabProjectManager {
    public partial class MainWindow : Window { //, IDisposable {
        private readonly Dictionary<int, ProjectGroups> allProjectGroups = new Dictionary<int, ProjectGroups>(); // Holds all project groups
        private readonly Dictionary<string, Project> allProjects = new Dictionary<string, Project>(); // Holds all projects
        private readonly bool isLoading = false; // flag that is used to prevent the Shared checkbox change event from being triggered
        bool isEditing = false;
        const string gitURL = "http://git.gms4sbc.com";
        const string privateToken = "bM262ELkCaAqz6TrWXMe";

        const string publickey = "Ek7Sp-l2";
        const string secretkey = "G$Hk%6l2";
        const string privatekey = "G$Hk%6l2";

        // Constructor
        public MainWindow() {
            InitializeComponent();

            lstProjectGroupSources.Items.Add("Gitlab Server");
            lstProjectGroupSources.Items.Add("Local DB");

            // Set status of Project Group Sources based on saved state from previous session
            lstProjectGroupSources.SelectedItem = Properties.Settings.Default.DefaultProjectGroupSource;

            // Prevents Shared checkbox change event from being triggered
            isLoading = true;

            // Set status of Shared checkbox based on saved state from previous session
            chkIncludeSharedProject.IsChecked = Properties.Settings.Default.SharedIsChecked;

            // Set status of Warn me if Shared project already exists checkbox based on saved state from previous session
            chkWarnSharedProjectExists.IsChecked = Properties.Settings.Default.WarnSharedProjectExists;

            // Set status of Close Automatically checkbox based on saved state from previous session
            chkCloseAutomatically.IsChecked = Properties.Settings.Default.CloseAutomatically;

            // Load Project group source - Setting this triggers LstProjectGroupSource_SelectionChanged() which loads the project groups and projects
            if (Properties.Settings.Default.DefaultProjectGroupSource != -1) {
                lstProjectGroupSources.SelectedIndex = Properties.Settings.Default.DefaultProjectGroupSource;

                lstProjectGroups.SelectedIndex = Properties.Settings.Default.DefaultProjectGroup;

                if (lstProjectGroups.SelectedIndex == -1 && lstProjectGroups.Items.Count > 0)
                    lstProjectGroups.SelectedItem = "Verj IO Projects";
            }

            this.isLoading = false;

            lstProjects.Focus(); // giving focus to the list of projects right away lets the user press a key to jump to a project name

            lstProjects.MouseDoubleClick += new MouseButtonEventHandler(LstProjects_MouseDoubleClick);
        }

        // Clone button click event
        private void BtnClone_Click(object sender, EventArgs e) {
            bool result = false;
            bool sharedFetched = false;

            if (lstProjects.SelectedItems.Count == 0 && chkIncludeSharedProject.IsChecked == false) {
                MessageBox.Show("Please select at least 1 project");
                return;
            }

            // Since you can select multiple projects, loop through each selected item in the listbox and fetch that project
            for (int projCounter = 0; projCounter < lstProjects.SelectedItems.Count; projCounter++) {
                if (lstProjects.SelectedItems[projCounter].ToString().Equals("Shared", StringComparison.CurrentCulture)) // If Shared was selected in the listbox, don't attempt to fetch it a 2nd time if the Shared checkbox is checked
                    sharedFetched = true;

                string selectedItem = lstProjects.SelectedItems[projCounter].ToString();

                // Fetch the current project
                result = FetchProject(this.allProjects[selectedItem].projectShortName, selectedItem);
            }

            // Get Shared project if checkbox is checked and Shared wasn't select in the list of packages because if it was it would have already been downloaded
            if (chkIncludeSharedProject.IsChecked == true && sharedFetched == false)
                result = FetchProject("shared", "Shared");

            if (result == true && chkCloseAutomatically.IsChecked == false) {
                MessageBox.Show("The selected project(s) are in your Verj IO Workspace.");
                return;
            }

            // Unselect all projects
            lstProjects.SelectedItems.Clear();

            if (chkCloseAutomatically.IsChecked == true)
                System.Windows.Application.Current.Shutdown();
        }

        // Populate the project groups listbox
        private void BuildProjectGroupListBox() {
            // this.Dispatcher.Invoke(() => is Needed to access lstProjectGroups because this method is called from a separate thread
            this.Dispatcher.Invoke(() => {
                isEditing = true;  // Prevents the selected event from being triggered

                lstProjectGroups.Items.Clear();

                lstProjectGroups.Items.Add("All"); // Add "All" to project groups dropdown

                // Get all project group names in an array so we can sort it
                string[] projectGroupNames = projectGroupNames = (from entry in this.allProjectGroups select entry.Value.projectGroupName).ToArray();

                Array.Sort(projectGroupNames);

                foreach (string item in projectGroupNames) {
                    lstProjectGroups.Items.Add(item);
                }

                this.isEditing = false;
            });
        }

        // Save state of close automatically checkbox
        private void ChkCloseAutomatically_CheckedChanged(object sender, EventArgs e) {
            // Whenever the shared checkbox is checked or unchecked, save this preference so it can be used when loading the application
            if (this.isLoading == false) {
                Properties.Settings.Default.CloseAutomatically = (bool)chkCloseAutomatically.IsChecked;
                Properties.Settings.Default.Save();
            }
        }

        // Save state of Warn me if Shared project already exists checkbox
        private void ChkWarnSharedProjectExists_CheckedChanged(object sender, EventArgs e) {
            // Whenever the shared checkbox is checked or unchecked, save this preference so it can be used when loading the application
            if (this.isLoading == false) {
                Properties.Settings.Default.WarnSharedProjectExists = (bool)chkWarnSharedProjectExists.IsChecked;
                Properties.Settings.Default.Save();
            }
        }

        // Enable or disable go button based on whether at least 1 project is selected
        private void ChkIncludeSharedProject_CheckedChanged(object sender, EventArgs e) {
            if (chkIncludeSharedProject.IsChecked == false)
                chkWarnSharedProjectExists.IsEnabled = false;
            else
                chkWarnSharedProjectExists.IsEnabled = true;

            EnableDisableGoButton();

            // Whenever the shared checkbox is checked or unchecked, save this preference so it can be used when loading the application
            if (this.isLoading == false) {
                Properties.Settings.Default.SharedIsChecked = (bool)chkIncludeSharedProject.IsChecked;
                Properties.Settings.Default.Save();
            }
        }

        static public string Decrypt(string textToDecrypt) {
            MemoryStream ms = null;
            CryptoStream cs = null;

            try {
                string ToReturn = "";

                byte[] privatekeyByte = Array.Empty<byte>();
                privatekeyByte = System.Text.Encoding.UTF8.GetBytes(privatekey);
                byte[] publickeybyte = Array.Empty<byte>();
                publickeybyte = System.Text.Encoding.UTF8.GetBytes(publickey);


                if (textToDecrypt != null && textToDecrypt.Length > 0) {
                    byte[] inputbyteArray = new byte[textToDecrypt.Replace(" ", "+").Length];

                    inputbyteArray = Convert.FromBase64String(textToDecrypt.Replace(" ", "+"));
                    DESCryptoServiceProvider des;

                    using (des = new DESCryptoServiceProvider()) {
                        ms = new MemoryStream();
                        cs = new CryptoStream(ms, des.CreateDecryptor(publickeybyte, privatekeyByte), CryptoStreamMode.Write);
                        cs.Write(inputbyteArray, 0, inputbyteArray.Length);
                        cs.FlushFinalBlock();
                        Encoding encoding = Encoding.UTF8;
                        ToReturn = encoding.GetString(ms.ToArray());
                    }

                    return ToReturn;
                }
            } catch (Exception ae) {
                throw new Exception(ae.Message, ae.InnerException);
            } finally {
                cs.Dispose();
            }

            return null;
        }

        // Enable or disable the Go button based on whether Include Shared project is checked or at least 1 project is selected
        private void EnableDisableGoButton() {
            if (lstProjects.SelectedItems.Count == 0 && chkIncludeSharedProject.IsChecked == false) {
                BtnClone.IsEnabled = false;
            } else if (lstProjects.SelectedItems.Count > 0 || (lstProjects.SelectedItems.Count == 0 && chkIncludeSharedProject.IsChecked == true)) {
                BtnClone.IsEnabled = true;
            }
        }

        static public string Encrypt(string textToEncrypt) {
            try {
                string ToReturn = "";

                byte[] secretkeyByte = Array.Empty<byte>();
                secretkeyByte = System.Text.Encoding.UTF8.GetBytes(secretkey);
                byte[] publickeybyte = Array.Empty<byte>();
                publickeybyte = System.Text.Encoding.UTF8.GetBytes(publickey);
                MemoryStream ms = null;
                CryptoStream cs = null;
                byte[] inputbyteArray = System.Text.Encoding.UTF8.GetBytes(textToEncrypt);


                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider()) {
                    ms = new MemoryStream();
                    cs = new CryptoStream(ms, des.CreateEncryptor(publickeybyte, secretkeyByte), CryptoStreamMode.Write);
                    cs.Write(inputbyteArray, 0, inputbyteArray.Length);
                    cs.FlushFinalBlock();
                    ToReturn = Convert.ToBase64String(ms.ToArray());
                }

                cs.Dispose();

                return ToReturn;
            } catch (Exception ex) {
                throw new Exception(ex.Message, ex.InnerException);
            }
        }

        // Fetch the given project
        private bool FetchProject(String projectKey, String fullProjectName) {
            bool showSharedAlerts = ((bool)chkWarnSharedProjectExists.IsChecked == false && fullProjectName.Equals("Shared", StringComparison.CurrentCulture) ? false : true);

            // The projects' group ID gives us access to the project group properties in this.allProjectGroups
            int projectGroupID = this.allProjects[fullProjectName].projectGroupID;
            string projectGroupURL = this.allProjectGroups[projectGroupID].baseURL;
            string projectPath = this.allProjectGroups[projectGroupID].projectPath;
            string projectName = this.allProjectGroups[projectGroupID].projectGroupName;

            // Validate required fields
            if (!Directory.Exists(projectPath)) {
                MessageBox.Show("The " + projectName + "path " + projectPath + " does not exist");
                return false;
            }

            // Make sure that the project folder doesn't exist already with the full project name
            if (showSharedAlerts && Directory.Exists(projectPath + fullProjectName + "\\")) {
                MessageBox.Show("There is already a folder called " + fullProjectName + " in " + projectPath + ". Please delete or rename this folder");
                return false;
            }

            // Make sure that the project folder doesn't exist already with the short project name because the git clone command will only create it if it doesn't exist already
            if (showSharedAlerts && projectGroupID == 4 && Directory.Exists(projectPath + projectKey + "\\")) {
                MessageBox.Show("There is already a folder called " + projectKey + " in " + projectPath + ". Please delete or rename this folder");
                return false;
            }

            // Execute command to clone the git repo
            var processStartInfo = new ProcessStartInfo() {
                WorkingDirectory = projectPath,

                FileName = "cmd.exe",

                Arguments = "/C git clone " + projectGroupURL + projectKey + ".git " + "\"" + fullProjectName + "\"",

                UseShellExecute = false,
            };

            Process proc = Process.Start(processStartInfo);

            proc.WaitForExit();

            // If this app is not being run on IntranetDev we are done
            if (!System.Net.Dns.GetHostName().Equals("IntranetDEV", StringComparison.CurrentCulture))
                return true;

            // When fetching the Shared project on IntranetDev, We need to delete all of the customer scheduled tasks because we don't want them to run on Intranet production and dev server
            if (projectKey.Equals("shared", StringComparison.CurrentCulture)) {
                String failedDeletes = "";
                String otherFilesCSV = "EmployeeRequisitions_NotApprovedByCLvlManager_Report.eb,Update_Kit_Planning_Update_Stats.eb";

                foreach (string f in Directory.EnumerateFiles(projectPath + projectKey + "\\Scheduled_Tasks", "Report_*.eb")) {
                    File.Delete(f);

                    if (File.Exists(projectPath + projectKey + "\\Scheduled_Tasks" + f)) {
                        failedDeletes = (failedDeletes.Length > 0 ? "," : "") + projectPath + projectKey + "\\Scheduled_Tasks" + f;
                    }
                }

                string[] otherFiles = otherFilesCSV.Split(',');

                foreach (var file in otherFiles) {
                    string fullFileName = projectPath + projectKey + "\\Scheduled_Tasks\\" + file;

                    File.Delete(fullFileName);

                    if (File.Exists(fullFileName)) {
                        failedDeletes = (failedDeletes.Length > 0 ? "," : "") + fullFileName;
                    }
                }

                if (failedDeletes.Length > 0)
                    MessageBox.Show("The following scheduled tasks could not be deleted and must be manually deleted: " + failedDeletes);
            }

            return true;
        }

        // Get the project group id of the specified project
        private int GetProjectGroupID(string projectGroupName) {
            // Return the first result
            return (from entry in this.allProjectGroups where entry.Value.projectGroupName.Equals(projectGroupName, StringComparison.CurrentCulture) select entry.Key).First();
        }

        // Load all of the projects in the selected project group
        private void LoadListBoxItemsForProjectGroupID() {
            this.Dispatcher.Invoke(() => {
                lstProjects.Items.Clear();

                foreach (string currProject in (lstProjectGroups.SelectedItem.Equals("All") ? this.allProjects.Keys : (from entry in this.allProjects where entry.Value.projectGroupID == GetProjectGroupID(lstProjectGroups.SelectedItem.ToString()) select entry.Key))) {
                    lstProjects.Items.Add(currProject);
                }

                // Sort all items
                lstProjects.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("", System.ComponentModel.ListSortDirection.Ascending));
            });
        }

        // Fetch all projects in the given project group from the REST resource
        private void LoadProjectsByGroupID(int projectGroupID) {
            string RESTURL = String.Concat(gitURL, "/api/v4/groups/", projectGroupID, "/projects?private_token=" + privateToken + "&per_page=9999&simple=true&sort=asc");

            IRestResponse response;

            var client = new RestClient(RESTURL);

            response = client.Execute(new RestRequest());

            if (response.Content.Length == 0) {
                MessageBox.Show("The git server at " + this.allProjectGroups[projectGroupID].baseURL + " did not return any projects and may not available");

                this.Dispatcher.Invoke(() => {
                    System.Windows.Application.Current.Shutdown();
                });

                return;
            }

            JArray allProjects = JArray.Parse(response.Content);

            // Loop through each project returned by the response and add to the dictionary
            for (int counter = 0; counter < allProjects.Count; counter++) {
                this.allProjects.Add(allProjects[counter]["name"].ToString(), new Project(allProjects[counter]["path"].ToString(), projectGroupID));
            }
        }

        // Read project groups from the DB
        private void LoadProjectGroupsFromDB() {
            String sql = "SELECT * FROM GitlabProjectManagerProjectGroups ORDER BY GitlabProjectManagerProjectGroupName";
            SqlCommand command;
            SqlDataReader dataReader;
            SqlConnection sqlConn;
            string path;

            const string info1 = "d0KD29IcCrVvLhDyQYt95w==";
            const string info2 = "GO2UJpkxXag6vNnVqv2CXA==";

            String connectionString = @"DATABASE = Intranet; SERVER = GENESIS,1433;;User ID=" + Decrypt(info1) + ";Password=" + Decrypt(info2);

            // Get connection based on the connection string
            sqlConn = new SqlConnection(connectionString);

            sqlConn.Open();

            command = new SqlCommand(sql, sqlConn);

            dataReader = command.ExecuteReader();

            this.allProjects.Clear();
            this.allProjectGroups.Clear();
            this.lstProjects.Items.Clear();

            // Read the result
            while (dataReader.Read()) {
                path = dataReader.GetValue(3).ToString();

                path = path.Replace("%USERPROFILE%", (Environment.GetEnvironmentVariable("USERPROFILE").Length > 0 ? Environment.GetEnvironmentVariable("USERPROFILE") : "C:\\"));

                this.LoadProjectsByGroupID(dataReader.GetInt32(0));

                this.allProjectGroups.Add(dataReader.GetInt32(0), new ProjectGroups(dataReader.GetValue(1).ToString(), dataReader.GetValue(2).ToString(), path));
            }

            sqlConn.Close();

            this.BuildProjectGroupListBox();

            command.Dispose();
        }

        // Load project group names from REST resource
        private void LoadProjectGroupsFromREST() {
            string RESTURL = String.Concat(gitURL, "/api/v4/groups/", "?private_token=" + privateToken);

            IRestResponse response;

            var client = new RestClient(RESTURL);

            response = client.Execute(new RestRequest());

            if (response.Content.Length == 0) {
                MessageBox.Show("The git server at " + RESTURL + " did not return any projects and may not available");

                this.Dispatcher.Invoke(() => {
                    System.Windows.Application.Current.Shutdown();
                });

                return;
            }

            JArray allProjectGroups = JArray.Parse(response.Content);

            this.allProjects.Clear();
            this.allProjectGroups.Clear();
            this.lstProjects.Items.Clear();

            // Loop through each project returned by the response and add to the dictionary
            for (int counter = 0; counter < allProjectGroups.Count; counter++) {
                int id = Int32.Parse(allProjectGroups[counter]["id"].ToString().Replace("{", "").Replace("}", ""), CultureInfo.CurrentCulture);
                string name = allProjectGroups[counter]["name"].ToString().Replace("{", "").Replace("}", "");
                string url = allProjectGroups[counter]["web_url"].ToString().Replace("{", "").Replace("}", "");
                string path = (name.Equals("Verj IO Projects", StringComparison.CurrentCulture) ? "C:\\VerjIOData\apps\\ufs\\workspace\\" : (Environment.GetEnvironmentVariable("USERPROFILE").Length > 0 ? Environment.GetEnvironmentVariable("USERPROFILE") + "\\Desktop" : "C:\\"));

                this.LoadProjectsByGroupID(id);
                this.allProjectGroups.Add(id, new ProjectGroups(name, url, path));
            }

            this.BuildProjectGroupListBox();
        }

        // Event when the project group selection changes
        private void LstProjectGroupSource_SelectionChanged(object sender, RoutedEventArgs e) {
            if (this.isLoading == false && lstProjectGroupSources.SelectedIndex != -1) {
                Properties.Settings.Default.DefaultProjectGroupSource = lstProjectGroupSources.SelectedIndex;
                Properties.Settings.Default.Save();
            }

            switch (lstProjectGroupSources.SelectedIndex) {
                case 0:  // Load from REST resource
                    LoadProjectGroupsFromREST();
                    break;
                case 1: // Load from DB
                    LoadProjectGroupsFromDB();
                    break;
            }
        }

        // When you press a key in the listbox, jump to the first project that begins with that letter
        private void LstProjects_KeyPressed(object sender, KeyEventArgs e) {
            // Get first matching item or null if there's no match
            var firstItem = (from entry in this.allProjects where entry.Key.ToLower(new CultureInfo("en-US", false)).StartsWith(e.Key.ToString().ToLower(new CultureInfo("en-US", false)), StringComparison.CurrentCulture) select entry.Key).FirstOrDefault();

            // Only set selected if a match was found. Otherwise stay on the previously selected item
            if (firstItem != null)
                lstProjects.SelectedItem = firstItem;
        }

        // Project double click event
        private void LstProjects_MouseDoubleClick(object sender, RoutedEventArgs e) {
            throw new NotImplementedException();
        }

        // Enable or disable go button based on whether at least 1 project is selected
        private void LstProjects_Selected(object sender, RoutedEventArgs e) {
            EnableDisableGoButton();
        }

        private void LstProjects_MouseDoubleClick(object sender, MouseEventArgs e) {
            //MessageBox.Show("You double clicked on a project");
            this.BtnClone_Click(new object(), new EventArgs());
        }

        // Project group changed
        private void LstProjectGroups_SelectionChanged(object sender, RoutedEventArgs e) {
            if (isEditing)
                return;

            if (lstProjectGroups.SelectedIndex != -1) {
                Properties.Settings.Default.DefaultProjectGroup = lstProjectGroups.SelectedIndex;
                Properties.Settings.Default.Save();
            }

            if (lstProjectGroups.SelectedIndex != -1) {
                this.LoadListBoxItemsForProjectGroupID();
            }

        }
    }
}