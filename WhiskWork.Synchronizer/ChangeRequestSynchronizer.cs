using System.Collections.Generic;
using System.Data;
using System.Linq;
using WhiskWork.Core;
using WhiskWork.Core.Synchronization;

namespace WhiskWork.Synchronizer
{
    public class ChangeRequestSynchronizer : EManagerWhiskWorkSynchronizer
    {
        private const int _maxOrdinal = 100000;

        public ChangeRequestSynchronizer(IWhiskWorkRepository whiskWorkRepository, IDominoRepository dominoRepository) : base(whiskWorkRepository, dominoRepository)
        {
        }

        public IEnumerable<string> ReleaseFilter { get; set; }
        public IEnumerable<string> TeamFilter { get; set; }

        protected override string WhiskWorkBeginStep
        {
            get { return "/cmsdev/scheduled"; }
        }

        protected override bool SynchronizeResponsibleEnabled
        {
            get { return true; }
        }

        protected override bool SynchronizeStatusReverseEnabled
        {
            get { return true; }
        }

        protected override SynchronizationMap CreateStatusMap()
        {
            var statusMap = new SynchronizationMap(EManagerAgent, WhiskWorkAgent);
            statusMap.AddReciprocalEntry("0a - Scheduled for development", WhiskWorkBeginStep);

            statusMap.AddReciprocalEntry("2 - Development", "/cmsdev/wip/analysis/inprocess");
            statusMap.AddReverseEntry("/cmsdev/wip/analysis/done", "2 - Development");
            statusMap.AddReverseEntry("/cmsdev/wip/development/inprocess", "2 - Development");
            statusMap.AddReciprocalEntry("3 - Ready for test", "/cmsdev/wip/development/done");
            statusMap.AddReverseEntry("/cmsdev/wip/feedback", "3 - Ready for test");
            statusMap.AddReverseEntry("/cmsdev/wip/feedback/review", "3 - Ready for test");
            statusMap.AddReverseEntry("/cmsdev/wip/feedback/test", "3 - Ready for test");

            statusMap.AddReciprocalEntry("4a ACCEPTED - In Dev", "/done");
            statusMap.AddForwardEntry("4a FAILED - In Dev", "/cmsdev/wip/development/inprocess");
            statusMap.AddReverseEntry("/test/inprocess", "4a ACCEPTED - In Dev");
            statusMap.AddReciprocalEntry("4b ACCEPTED - In Test", "/test/done");
            statusMap.AddForwardEntry("4b FAILED - In Test", "/cmsdev/wip/development/inprocess");
            statusMap.AddReverseEntry("/stage", "4b ACCEPTED - In Test");
            statusMap.AddReciprocalEntry("5 - Approved (ready for deploy)", "/approved");
            statusMap.AddForwardEntry("4c ACCEPTED - In Stage", "/approved");
            statusMap.AddForwardEntry("4c FAILED - In Stage", "/cmsdev/wip/development/inprocess");
            statusMap.AddReciprocalEntry("7 - Deployed to prod", "/deployed");


            statusMap.AddReciprocalEntry("0b - Scheduled for analysis", "/cmsanalysis/scheduled");
            statusMap.AddReciprocalEntry("1 - Analysis", "/cmsanalysis/inprocess");
            return statusMap;
        }

        protected override DataSynchronizer CreatePropertyMap()
        {
            var propertyMap = new SynchronizationMap(EManagerAgent, WhiskWorkAgent);
            propertyMap.AddReciprocalEntry("name", "name");
            propertyMap.AddReciprocalEntry("unid", "unid");
            propertyMap.AddReciprocalEntry("title", "title");
            propertyMap.AddReciprocalEntry("team", "team");
            propertyMap.AddReciprocalEntry("release", "release");
            propertyMap.AddReciprocalEntry("project", "project");
            propertyMap.AddReciprocalEntry("leanstatus", "leanstatus");
            propertyMap.AddReciprocalEntry("priority", "priority");
            var propertySynchronizer = new DataSynchronizer(propertyMap, EManagerAgent, WhiskWorkAgent);
            propertySynchronizer.SynchronizeOrdinal = true;
            return propertySynchronizer;
        }

        protected override IEnumerable<SynchronizationEntry> MapFromWhiskWork(IEnumerable<WorkItem> workItems)
        {
            var crs = workItems.Where(wi => wi.Classes.Contains("cr") && !wi.Id.StartsWith("B") && !wi.Id.StartsWith("S") && !wi.Id.StartsWith("T"));
            var normalCrs = crs.Where(wi => !wi.Classes.Contains("cr-review") && !wi.Classes.Contains("cr-test"));
            return normalCrs.Select(wi => SynchronizationEntry.FromWorkItem(wi));
        }

        protected override SynchronizationEntry MapFromEManager(DataRow dataRow)
        {
            var release = (string)dataRow[0];
            var leanStatus = (string)dataRow[1];
            var team = (string)dataRow[2];
            var id = (string)dataRow[3];
            var title = (string)dataRow[4];
            var project = (string)dataRow[5];
            var unid = (string)dataRow[6];
            var status = (string)dataRow[7];
            var ordinal = ParseNumber((string)dataRow[8]);
            var person = (string)dataRow[9];
            var timeStamp = ParseDominoTimeStamp((string)dataRow[10]);
            
            if ((TeamFilter != null && !TeamFilter.Contains(team)) || (ReleaseFilter != null && !ReleaseFilter.Contains(release)))
            {
                return null;
            }

            if (string.IsNullOrEmpty(id) || id == "0")
            {
                return null;
            }

            var properties =
                new Dictionary<string, string>
                    {
                        {"name", id},
                        {"unid", unid},
                        {"title", title},
                        {"team", team},
                        {"release", release},
                        {"project", project},
                        {"leanstatus",leanStatus},
                        {"priority",!ordinal.HasValue ? "undefined" : ordinal.ToString()},
                    };

            if (!string.IsNullOrEmpty(person))
            {
                properties.Add("CurrentPerson", person);
            }

            if(!ordinal.HasValue)
            {
                ordinal = _maxOrdinal;
            }

            return new SynchronizationEntry(id, status, properties) { Ordinal = ordinal, TimeStamp = timeStamp};
        }
    }
}