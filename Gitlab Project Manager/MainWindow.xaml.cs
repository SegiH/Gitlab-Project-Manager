using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace GitlabProjectManager {
    public partial class MainWindow : Window {
        private readonly Dictionary<int, ProjectGroups> allProjectGroups = new Dictionary<int, ProjectGroups>(); // Holds all project groups
        private readonly Dictionary<string, Project> allProjects = new Dictionary<string, Project>(); // Holds all projects
        private readonly bool isLoading = false; // flag that is used to prevent the Shared checkbox change event from being triggered
        bool isEditing = false;

        // Constructor
        public MainWindow() {
            InitializeComponent();

            // Prevents Shared checkbox change event from being triggered
            isLoading = true;

            /*** Load Settings ***/
            // Set status of Shared checkbox based on saved state from previous session
            chkIncludeSharedProject.IsChecked = Properties.Settings.Default.SharedIsChecked;

            // Set status of Warn me if Shared project already exists checkbox based on saved state from previous session
            chkWarnSharedProjectExists.IsChecked = Properties.Settings.Default.WarnSharedProjectExists;

            // Set status of Close Automatically checkbox based on saved state from previous session
            chkCloseAutomatically.IsChecked = Properties.Settings.Default.CloseAutomatically;

            TxtGitURL.Text = Properties.Settings.Default.GitURL;

            
            TxtPrivateKey.Password = Properties.Settings.Default.PrivateKey;
            /*** Load Settings ***/


            // Load projects from REST service if the URL and private key were previously saved
            LoadProjects(true);

            // If the Shared project doesn't exist, disable it
            try {
                var shared=this.allProjects["Shared"];
            } catch (System.Collections.Generic.KeyNotFoundException) {
                chkIncludeSharedProject.IsChecked = false;
                chkIncludeSharedProject.IsEnabled = false;
            }

            this.isLoading = false;            

            // Project list mouse click event handler
            lstProjects.MouseDoubleClick += new MouseButtonEventHandler(LstProjects_MouseDoubleClick);
        }

        /*** EVENTS ***/

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

        // Save state of close automatically checkbox
        private void ChkCloseAutomatically_CheckedChanged(object sender, EventArgs e) {
            // Whenever the shared checkbox is checked or unchecked, save this preference so it can be used when loading the application
            if (this.isLoading == false) {
                Properties.Settings.Default.CloseAutomatically = (bool)chkCloseAutomatically.IsChecked;
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

        // Save state of Warn me if Shared project already exists checkbox
        private void ChkWarnSharedProjectExists_CheckedChanged(object sender, EventArgs e) {
            // Whenever the shared checkbox is checked or unchecked, save this preference so it can be used when loading the application
            if (this.isLoading == false) {
                Properties.Settings.Default.WarnSharedProjectExists = (bool)chkWarnSharedProjectExists.IsChecked;
                Properties.Settings.Default.Save();
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

        // Project group changed
        private void LstProjectGroups_SelectionChanged(object sender, RoutedEventArgs e) {
            if (isEditing)
                return;

            if (lstProjectGroups.SelectedIndex != -1) {
                Properties.Settings.Default.DefaultProjectGroup = lstProjectGroups.SelectedIndex;
                Properties.Settings.Default.Save();
            }

            if (lstProjectGroups.SelectedIndex != -1)
                this.LoadListBoxItemsForProjectGroupID();
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
            this.BtnClone_Click(new object(), new EventArgs());
        }

        // togglePrivateKey_MouseDown
        private void TogglePrivateKey_MouseDown(object sender, RoutedEventArgs e) {
            if (imgShowPrivateKey.IsVisible == true) {
                imgShowPrivateKey.Visibility = Visibility.Hidden;
                imgHidePrivateKey.Visibility = Visibility.Visible;

                TxtPrivateKeyVisible.Text = TxtPrivateKey.Password;
                TxtPrivateKeyVisible.Visibility = Visibility.Visible;
            } else {
                imgShowPrivateKey.Visibility = Visibility.Visible;
                imgHidePrivateKey.Visibility = Visibility.Hidden;

                TxtPrivateKeyVisible.Text = "";
                TxtPrivateKeyVisible.Visibility = Visibility.Hidden;
            }
        }

        // When the user searches, immediately fill in the text field
        private void TxtSearch_TextChanged(object sender, RoutedEventArgs e) {
            LoadListBoxItemsForProjectGroupID();
        }

        private void TxtGitURL_TextChanged(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.GitURL = TxtGitURL.Text;
            Properties.Settings.Default.Save();

            if (!this.isLoading)
                LoadProjects();
        }

        private void TxtPrivateKey_PasswordChanged(object sender, RoutedEventArgs args) {
            Properties.Settings.Default.PrivateKey = TxtPrivateKey.Password;
            Properties.Settings.Default.Save();

            if (!this.isLoading)
                 LoadProjects();
        }

        /*** User defined functions ***/

        // Populate the project groups listbox
        private void BuildProjectGroupListBox() {
            // this.Dispatcher.Invoke(() => is Needed to access lstProjectGroups because this method is called from a separate thread
            this.Dispatcher.Invoke(() => {
                isEditing = true;  // Prevents the selected event from being triggered

                lstProjectGroups.Items.Clear();

                lstProjectGroups.Items.Add("All"); // Add "All" to project groups dropdown

                // Make All the default selected option
                lstProjectGroups.SelectedIndex = 0;

                // Get all project group names in an array so we can sort it
                string[] projectGroupNames = (from entry in this.allProjectGroups select entry.Value.projectGroupName).ToArray();

                Array.Sort(projectGroupNames);

                foreach (string item in projectGroupNames)
                    lstProjectGroups.Items.Add(item);

                this.isEditing = false;

                this.LoadListBoxItemsForProjectGroupID();
            });
        }

        // Enable or disable the Go button based on whether Include Shared project is checked or at least 1 project is selected
        private void EnableDisableGoButton() {
            if (lstProjects.SelectedItems.Count == 0 && chkIncludeSharedProject.IsChecked == false)
                BtnClone.IsEnabled = false;
            else if (lstProjects.SelectedItems.Count > 0 || (lstProjects.SelectedItems.Count == 0 && chkIncludeSharedProject.IsChecked == true))
                BtnClone.IsEnabled = true;
        }

        // Fetch the given project
        private bool FetchProject(String projectKey, String fullProjectName) {
            bool showSharedAlerts = ((bool)chkWarnSharedProjectExists.IsChecked != false || !fullProjectName.Equals("Shared", StringComparison.CurrentCulture));

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

            if (projectGroupURL.StartsWith("http://", StringComparison.CurrentCulture)) {
                var allowSSLProcessStartInfo = new ProcessStartInfo() {
                    WorkingDirectory = projectPath,

                    FileName = "cmd.exe",

                    Arguments = "/C " + "git clone " + projectGroupURL.Replace("/groups/", "/") + "/" + projectKey + ".git " + "\"" + fullProjectName + "\"",

                    UseShellExecute = false,
                };

                try {
                    Process allowSSLProc = Process.Start(allowSSLProcessStartInfo);

                    allowSSLProc.WaitForExit();
                } catch (Exception e) {
                    MessageBox.Show("An error occurred ");
                    return false;
                }                
            }

            // Execute command to clone the git repo
            var processStartInfo = new ProcessStartInfo() {
                WorkingDirectory = projectPath,

                FileName = "cmd.exe",

                Arguments = "/C " + "git clone " + projectGroupURL.Replace("/groups/","/") + "/" + projectKey + ".git " + "\"" + fullProjectName + "\"",

                UseShellExecute = false,
            };

            try {
            } catch (Exception e) {
                Process proc = Process.Start(processStartInfo);

                proc.WaitForExit();
            }
                

            // If this app is not being run on IntranetDev we are done
            if (!System.Net.Dns.GetHostName().Equals("IntranetDEV", StringComparison.CurrentCulture))
                return true;

            // When fetching the Shared project on IntranetDev, We need to delete all of the customer scheduled tasks because we don't want them to run on Intranet production and dev server
            if (projectKey.Equals("shared", StringComparison.CurrentCulture)) {
                String failedDeletes = "";
                String otherFilesCSV = "EmployeeRequisitions_NotApprovedByCLvlManager_Report.eb,Update_Kit_Planning_Update_Stats.eb";

                foreach (string f in Directory.EnumerateFiles(projectPath + projectKey + "\\Scheduled_Tasks", "Report_*.eb")) {
                    File.Delete(f);

                    if (File.Exists(projectPath + projectKey + "\\Scheduled_Tasks" + f))
                        failedDeletes = (failedDeletes.Length > 0 ? "," : "") + projectPath + projectKey + "\\Scheduled_Tasks" + f;
                }

                string[] otherFiles = otherFilesCSV.Split(',');

                foreach (var file in otherFiles) {
                    string fullFileName = projectPath + projectKey + "\\Scheduled_Tasks\\" + file;

                    File.Delete(fullFileName);

                    if (File.Exists(fullFileName))
                        failedDeletes = (failedDeletes.Length > 0 ? "," : "") + fullFileName;
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

        // Load projects if Git URL and private key were previously saved
        private void LoadProjects(bool isLoading = false) {
            if (TxtGitURL.Text.Length > 0 && TxtPrivateKey.Password.Length > 0) {
                LoadProjectGroupsFromREST();

                lstProjects.Focus(); // giving focus to the list of projects right away lets the user press a key to jump to a project name
            } else if (isLoading) {
                if (TxtGitURL.Text.Length == 0 && TxtPrivateKey.Password.Length == 0)
                    MessageBox.Show("Please enter the Git URL and private key");
                else if (TxtGitURL.Text.Length == 0)
                    MessageBox.Show("Please enter the Git URL");
                else if (TxtPrivateKey.Password.Length == 0)
                    MessageBox.Show("Please enter the private key");
            }
        }

        // Load all of the projects in the selected project group
        private void LoadListBoxItemsForProjectGroupID() {
            this.Dispatcher.Invoke(() => {
                lstProjects.Items.Clear();

                foreach (string currProject in (lstProjectGroups.SelectedItem.Equals("All") ? this.allProjects.Keys : (from entry in this.allProjects where entry.Value.projectGroupID == GetProjectGroupID(lstProjectGroups.SelectedItem.ToString()) select entry.Key))) {
                    if (TxtSearch.Text.Length == 0 || (TxtSearch.Text.Length > 0 && currProject.ToLower(new CultureInfo("en-US", false)).Contains(TxtSearch.Text.ToLower(new CultureInfo("en-US", false)))))
                        lstProjects.Items.Add(currProject);
                }

                // Sort all items
                lstProjects.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("", System.ComponentModel.ListSortDirection.Ascending));
            });
        }

        // Fetch all projects in the given project group from the REST resource
        private void LoadProjectsByGroupID(int projectGroupID) {
            string RESTURL = String.Concat(TxtGitURL.Text, "/api/v4/groups/", projectGroupID, "/projects?private_token=" + TxtPrivateKey.Password + "&per_page=9999&simple=true&sort=asc");

            IRestResponse response;

            var client = new RestClient(RESTURL);

            response = client.Execute(new RestRequest());

            if (response.Content.Length == 0) {
                MessageBox.Show("The git server at " + this.allProjectGroups[projectGroupID].baseURL + " did not return any projects and may not available");

                return;
            }

            JArray allProjects = JArray.Parse(response.Content);

            // Loop through each project returned by the response and add to the dictionary
            for (int counter = 0; counter < allProjects.Count; counter++)
                this.allProjects.Add(allProjects[counter]["name"].ToString(), new Project(allProjects[counter]["path"].ToString(), projectGroupID));
        }

        // Load project group names from REST resource
        private void LoadProjectGroupsFromREST() {
            string RESTURL = String.Concat(TxtGitURL.Text, "/api/v4/groups/", "?private_token=" + TxtPrivateKey.Password);

            IRestResponse response;

            var client = new RestClient(RESTURL);

            response = client.Execute(new RestRequest());

            if (response.StatusCode.ToString() != "OK") {
                MessageBox.Show("The git server at " + TxtGitURL.Text + " returned the status code " + response.StatusCode + " with the error \"" + response.ErrorMessage + "\"");
                return;
            }

            if (response.Content.Length == 0) {
                MessageBox.Show("The git server at " + TxtGitURL.Text + " did not return any projects and may not available");
                return;
            }

            if (response.Content.Contains("<html>")) {
                MessageBox.Show("The git server at " + TxtGitURL.Text + " did not return a valid response");
                return;
            }

            /*try {
                var responseJSON = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response.Content);

                if (responseJSON["message"] != null) {
                    MessageBox.Show("The git server at " + TxtGitURL.Text + " responded with the error " + responseJSON["message"]);
                    return;
                }
            } catch (JsonException j) {
                MessageBox.Show("The git server at " + TxtGitURL.Text + " responded with the error " + j + " while parsing the response");
                return;
            }*/

            /*if (response.Content.Contains("\"message\"")) {
                MessageBox.Show("The git server at " + TxtGitURL.Text + " responded with the error " + Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response.Content)["message"]);
                return;
            }*/

            JArray allProjectGroups = JArray.Parse(response.Content);

            this.allProjects.Clear();
            this.allProjectGroups.Clear();
            this.lstProjects.Items.Clear();

            // Loop through each project returned by the response and add to the dictionary
            for (int counter = 0; counter < allProjectGroups.Count; counter++) {
                int id = Int32.Parse(allProjectGroups[counter]["id"].ToString().Replace("{", "").Replace("}", ""), CultureInfo.CurrentCulture);
                string name = allProjectGroups[counter]["name"].ToString().Replace("{", "").Replace("}", "");
                string url = allProjectGroups[counter]["web_url"].ToString().Replace("{", "").Replace("}", "");
                string path = (name.Equals("Verj IO Projects", StringComparison.CurrentCulture) ? "C:\\VerjIOData\\apps\\ufs\\workspace\\" : (Environment.GetEnvironmentVariable("USERPROFILE").Length > 0 ? Environment.GetEnvironmentVariable("USERPROFILE") + "\\Desktop" : "C:\\"));

                this.LoadProjectsByGroupID(id);
                this.allProjectGroups.Add(id, new ProjectGroups(name, url, path));
            }

            this.BuildProjectGroupListBox();
        }
    }
}
