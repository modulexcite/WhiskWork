using System;
using System.Linq;

namespace WhiskWork.Core.Synchronization
{
    public class StatusSynchronizer
    {
        private readonly ISynchronizationAgent _master;
        private readonly ISynchronizationAgent _slave;
        private readonly StatusSynchronizationMap _map;

        public StatusSynchronizer(StatusSynchronizationMap map, ISynchronizationAgent master, ISynchronizationAgent slave)
        {
            _map = map;
            _master = master;
            _slave = slave;
        }

        public void Synchronize()
        {
            var masterEntries = _master.GetAll().ToDictionary(e=>e.Id);
            var slaveEntries = _slave.GetAll().ToDictionary(e=>e.Id);

            foreach(var masterId in masterEntries.Keys)
            {
                if(slaveEntries.ContainsKey(masterId))
                {
                    SynchronizationEntry masterMappedSlaveEntry;
                    if(!TryGetMasterEntry(slaveEntries[masterId],out masterMappedSlaveEntry))
                    {
                        continue;
                    }

                    if (masterMappedSlaveEntry.Status != masterEntries[masterId].Status)
                    {
                        SynchronizationEntry slaveEntry;
                        if (TryGetSlaveEntry(masterEntries[masterId], out slaveEntry))
                        {
                            _slave.UpdateStatus(slaveEntry);
                        }
                    }
                }
            }
        }

        private bool TryGetSlaveEntry(SynchronizationEntry masterEntry, out SynchronizationEntry slaveEntry)
        {
            if (!_map.ContainsKey(_master, masterEntry.Status))
            {
                slaveEntry = null;
                return false;
            }

            
            var slaveStatus = _map.GetMappedValue(_master, masterEntry.Status);

            slaveEntry = new SynchronizationEntry(masterEntry.Id, slaveStatus, masterEntry.Properties);
            return true;
        }

        private bool TryGetMasterEntry(SynchronizationEntry slaveEntry, out SynchronizationEntry masterEntry)
        {
            if(!_map.ContainsKey(_slave, slaveEntry.Status))
            {
                masterEntry = null;
                return false;
            }
            
            var masterStatus = _map.GetMappedValue(_slave, slaveEntry.Status);

            masterEntry = new SynchronizationEntry(slaveEntry.Id, masterStatus, slaveEntry.Properties);
            return true;
        }
        
    }
}