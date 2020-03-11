using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Threading;
using System.Windows.Input;

namespace GMS_Gitlab_Project_Manager {
    public partial class MainWindow : Window {
        private Dictionary<int, ProjectGroups> allProjectGroups = new Dictionary<int, ProjectGroups>(); // Holds all project groups
        private Dictionary<string, Project> allProjects = new Dictionary<string, Project>(); // Holds all projects
        private bool isLoading=false; // flag that is used to prevent the Shared checkbox change event from being triggered
        bool isEditing = false;
        Thread loadProjectThread;

        // Constructor
        public MainWindow() {
            InitializeComponent();

            // Init the project groups - This can't be automated since I have to specify the path where to save the project for each project group
            this.allProjectGroups.Add(7, new ProjectGroups("Angular projects", "http://git.gms4sbc.com/angular-projects/", (Environment.GetEnvironmentVariable("USERPROFILE") != "" ? Environment.GetEnvironmentVariable("USERPROFILE") + "\\Desktop" : "C:\\")));
            this.allProjectGroups.Add(4,new ProjectGroups("Verj IO projects", "http://git.gms4sbc.com/verj-io-projects/", "C:\\VerjIOData\\apps\\ufs\\workspace\\"));
            this.allProjectGroups.Add(8, new ProjectGroups("C# Projects", "http://git.gms4sbc.com/c-projects/", (Environment.GetEnvironmentVariable("USERPROFILE") != "" ? Environment.GetEnvironmentVariable("USERPROFILE") + "\\Desktop" : "C:\\")));
            this.allProjectGroups.Add(9, new ProjectGroups("VBA Projects", "http://git.gms4sbc.com/vba-projects/", (Environment.GetEnvironmentVariable("USERPROFILE") != "" ? Environment.GetEnvironmentVariable("USERPROFILE") + "\\Desktop" : "C:\\")));
            //
            // Prevents Shared checkbox change event from being triggered
            isLoading = true;

            // Set status of Shared checkbox based on saved state from previous session
            chkIncludeSharedProject.IsChecked = Properties.Settings.Default.SharedIsChecked;

            // Set status of Close Automatically checkbox based on saved state from previous session
            chkCloseAutomatically.IsChecked = Properties.Settings.Default.CloseAutomatically;

            this.isLoading = false;

            lstProjects.Focus(); // giving focus to the list of projects right away lets the user press a key to jump to a project name
        }

        // Clone button click event
        private void BtnClone_Click(object sender, EventArgs e) {
            int projCounter = 0;
            bool result = false;
            bool sharedFetched = false;

            if (lstProjects.SelectedItems.Count == 0 && chkIncludeSharedProject.IsChecked == false) {
                MessageBox.Show("Please select at least 1 project");
                return;
            }

            // Since you can select multiple projects, loop through each selected item in the listbox and fetch that project
            for (projCounter = 0; projCounter<lstProjects.SelectedItems.Count;projCounter++) {                
                if (lstProjects.SelectedItems[projCounter].ToString().Equals("Shared")) // If Shared was selected in the listbox, don't attempt to fetch it a 2nd time if the Shared checkbox is checked
                    sharedFetched = true;

                string selectedItem = lstProjects.SelectedItems[projCounter].ToString();

                // Fetch the current project
                result = fetchProject(this.allProjects[selectedItem].projectShortName, selectedItem);
            }

            // Get Shared project if checkbox is checked and Shared wasn't select in the list of packages because if it was it would have already been downloaded
            if (chkIncludeSharedProject.IsChecked == true && sharedFetched == false)
                result = fetchProject("shared", "Shared");

            if (result == true && chkCloseAutomatically.IsChecked == false) {
                MessageBox.Show("The selected project(s) are in your Verj IO Workspace.");
                return;
            }

            // Unselect all projects
            lstProjects.SelectedItems.Clear();

            if (chkCloseAutomatically.IsChecked == true)
                System.Windows.Application.Current.Shutdown();
        }

        // Enable or disable go button based on whether at least 1 project is selected
        private void ChkIncludeSharedProject_CheckedChanged(object sender, EventArgs e) {
            enableDisableGoButton();

            // Whenever the shared checkbox is checked or unchecked, save this preference so it can be used when loading the application
            if (this.isLoading == false) {
                 Properties.Settings.Default.SharedIsChecked = (bool)chkIncludeSharedProject.IsChecked;
                 Properties.Settings.Default.Save();
            }           
        }

        // Enable or disable button based on whether at least 1 project is selected
        private void ChkCloseAutomatically_CheckedChanged(object sender, EventArgs e) {
            // Whenever the shared checkbox is checked or unchecked, save this preference so it can be used when loading the application
            if (this.isLoading == false) {
                Properties.Settings.Default.CloseAutomatically = (bool)chkCloseAutomatically.IsChecked;
                Properties.Settings.Default.Save();
            }
        }

        // When you press a key in the listbox, jump to the first project that begins with that letter
        private void LstProjects_KeyPressed(object sender, KeyEventArgs e) {
            // Get first matching item or null if there's no match
            var firstItem = (from entry in this.allProjects where entry.Key.ToString().ToLower().StartsWith(e.Key.ToString().ToLower()) select entry.Key).FirstOrDefault();

            // Only set selected if a match was found. Otherwise stay on the previously selected item
            if (firstItem != null)
                lstProjects.SelectedItem = firstItem;
        }

        // Enable or disable go button based on whether at least 1 project is selected
        private void LstProjects_Selected(object sender, RoutedEventArgs e) {
            enableDisableGoButton();
        }

        // Project group changed
        private void LstProjectGroups_SelectionChanged(object sender, RoutedEventArgs e) {
            if (isEditing)
                return;

            loadListBoxItemsForProjectGroupID();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            // Start a new thread to Load all projects so the UI doesn't lock up while its loading
            this.loadProjectThread = new Thread(new ThreadStart(this.loadProjects));
            loadProjectThread.Start();
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e) {
            this.loadProjectThread.Abort();
        }

        // Enable or disable the Go button based on whether Include Shared project is checked or at least 1 project is selected
        private void enableDisableGoButton() {
            if (lstProjects.SelectedItems.Count == 0 && chkIncludeSharedProject.IsChecked == false)  {
                BtnClone.IsEnabled = false;
            } else if (lstProjects.SelectedItems.Count > 0 || (lstProjects.SelectedItems.Count == 0 && chkIncludeSharedProject.IsChecked == true)) {
                BtnClone.IsEnabled = true;
            }
        }

        // Fetch the given project
        private bool fetchProject(String projectKey, String fullProjectName) {
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
            if (Directory.Exists(projectPath + fullProjectName + "\\")) {
                MessageBox.Show("There is already a folder called " + fullProjectName + " in " + projectPath + ". Please delete or rename this folder");
                return false;
            }

            // Make sure that the project folder doesn't exist already with the short project name because the git clone command will only create it if it doesn't exist already
            if (projectGroupID == 4 && Directory.Exists(projectPath + projectKey + "\\")) {
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
            if (!System.Net.Dns.GetHostName().Equals("IntranetDEV"))
                return true;

            // When fetching the Shared project on IntranetDev, We need to delete all of the customer scheduled tasks because we don't want them to run on Intranet production and dev server
            if (projectKey.Equals("shared")) {
                String failedDeletes = "";
                String otherFilesCSV = "EmployeeRequisitions_NotApprovedByCLvlManager_Report.eb,Update_Kit_Planning_Update_Stats.eb";

                foreach (string f in Directory.EnumerateFiles(projectPath + projectKey + "\\Scheduled_Tasks", "Report_*.eb")) {
                     File.Delete(f);

                     if (File.Exists(projectPath + projectKey + "\\Scheduled_Tasks" + f)) {
                         failedDeletes = (!failedDeletes.Equals("") ? "," : "") + projectPath + projectKey + "\\Scheduled_Tasks" + f;
                     }
                }

                string[] otherFiles = otherFilesCSV.Split(',');

                foreach (var file in otherFiles) {
                    string fullFileName = projectPath + projectKey + "\\Scheduled_Tasks\\" + file;

                    File.Delete(fullFileName);

                    if (File.Exists(fullFileName)) {
                        failedDeletes = (!failedDeletes.Equals("") ? "," : "") + fullFileName;
                    }
                }

                if (!failedDeletes.Equals(""))
                    MessageBox.Show("The following scheduled tasks could not be deleted and must be manually deleted: " + failedDeletes);
            }

            return true;
        }

        // Get the project group id of the specified project
        private int getProjectGroupID(string projectGroupName) {
            // Return the first result
            return (from entry in this.allProjectGroups where entry.Value.projectGroupName.Equals(projectGroupName) select entry.Key).First();
        }

        // Load all project in the selected project group
        private void loadListBoxItemsForProjectGroupID() {
            this.Dispatcher.Invoke(() => {
                lstProjects.Items.Clear();

                if (lstProjectGroups.SelectedItem.ToString().Equals("All")) {
                    foreach (string key in this.allProjects.Keys) {
                        lstProjects.Items.Add(key);
                    }
                } else {
                    // select all project names in the specified project group
                    var projects = from entry in this.allProjects where entry.Value.projectGroupID == getProjectGroupID(lstProjectGroups.SelectedItem.ToString()) select entry.Key;

                    foreach (string currProject in projects) {
                        lstProjects.Items.Add(currProject);
                    }
                }

                // Sort all items
                lstProjects.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("", System.ComponentModel.ListSortDirection.Ascending));
            });
        }

        // Loads all projects groups and all projects for the default project group
        private void loadProjects() {
            // this.Dispatcher.Invoke(() => is Needed to access lstProjectGroups because this method is called from a separate thread
            this.Dispatcher.Invoke(() => {
                // Add "All" to project groups dropdown
                isEditing = true;
                lstProjectGroups.Items.Add("All");
                this.isEditing = false;

                // Get all project group names in an array so we can sort it
                string[] projectGroupNames = projectGroupNames = (from entry in this.allProjectGroups select entry.Value.projectGroupName).ToArray();
                
                Array.Sort(projectGroupNames);

                foreach (string item in projectGroupNames) {
                    isEditing = true; // Prevents the event from being triggered
                    lstProjectGroups.Items.Add(item);

                    if (item.Equals("Verj IO projects")) {
                        lstProjectGroups.SelectedItem = item;
                    }

                    this.isEditing = false;

                    // Load all projects for the specified project group id
                    loadProjectsByGroupID(getProjectGroupID(item));
                }
            });

            // Since the project group dropdown has a preselected value, display all projects for the selected project
            this.loadListBoxItemsForProjectGroupID();
        }

        // Fetch all projects in the given project group
        private void loadProjectsByGroupID(int projectGroupID)  {
            string RESTURL = String.Concat("http://git.gms4sbc.com/api/v4/groups/", projectGroupID, "/projects?private_token=bM262ELkCaAqz6TrWXMe&per_page=9999&simple=true&sort=asc");

            IRestResponse response;

            var client = new RestClient(RESTURL);            

            response = client.Execute(new RestRequest());

            if (response.Content.Equals("")) {
                MessageBox.Show("The git server at " + this.allProjectGroups[projectGroupID].baseURL + " did not return any projects and may not available");

                this.Dispatcher.Invoke(() => {
                    System.Windows.Application.Current.Shutdown();
                });

                return;
            }

            JArray allProjects = JArray.Parse(response.Content);

            // Loop through each project returned by the response and add to the dictionary
            for (int counter=0;counter<allProjects.Count;counter++) {
                this.allProjects.Add(allProjects[counter]["name"].ToString(), new Project(allProjects[counter]["path"].ToString(), projectGroupID));
            }
        }
    }

    /*class Encrypt {
        // This size of the IV (in bytes) must = (keysize / 8).  Default keysize is 256, so the IV must be
        // 32 bytes long.  Using a 16 character string here gives us 32 bytes when converted to a byte array.
        private const string initVector = "pemgail9uzpgzl88";
        // This constant is used to determine the keysize of the encryption algorithm
        private const int keysize = 256;

        //Encrypt
        public static string EncryptString(string plainText, string passPhrase) {
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged() {
                Mode = CipherMode.CBC
            };
            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            byte[] cipherTextBytes = memoryStream.ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return Convert.ToBase64String(cipherTextBytes);
        }

        //Decrypt
        public static string DecryptString(string cipherText, string passPhrase) {
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged()
            {
                Mode = CipherMode.CBC
            };
            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream(cipherTextBytes);
            CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            byte[] plainTextBytes = new byte[cipherTextBytes.Length];
            int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
            memoryStream.Close();
            cryptoStream.Close();
            return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
        }
    }*/

    class Project {
        public string projectShortName { get; set; }

        public int projectGroupID { get; set; }

        public Project(string _projectShortName, int _projectGroupID) {
            this.projectShortName = _projectShortName;
            this.projectGroupID = _projectGroupID;
        }
    }

    class ProjectGroups {
        public string projectGroupName { get; set; }
        public string baseURL { get; set; }
        public string projectPath { get; set; }

        public ProjectGroups(string _projectGroupName, string _baseURL, string _projectPath) {
            this.projectGroupName = _projectGroupName;
            this.baseURL = _baseURL;
            this.projectPath = _projectPath;
        }
    }
}