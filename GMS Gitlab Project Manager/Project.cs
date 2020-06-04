using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMSGitlabProjectManager {
    class Project {
        public string projectShortName { get; set; }

        public int projectGroupID { get; set; }

        public Project(string _projectShortName, int _projectGroupID) {
            this.projectShortName = _projectShortName;
            this.projectGroupID = _projectGroupID;
        }
    }
}
