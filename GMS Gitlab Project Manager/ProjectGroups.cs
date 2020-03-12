using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMS_Gitlab_Project_Manager {
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
