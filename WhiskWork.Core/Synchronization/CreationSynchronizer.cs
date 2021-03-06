using System;
using System.Collections.Generic;
using System.Linq;

namespace WhiskWork.Core.Synchronization
{
    public class CreationSynchronizer
    {
        private readonly ISynchronizationAgent _master;
        private readonly ISynchronizationAgent _slave;
        private readonly SynchronizationMap _map;

        public CreationSynchronizer(SynchronizationMap map, ISynchronizationAgent master, ISynchronizationAgent slave)
        {
            _map = map;
            _master = master;
            _slave = slave;
        }

        public void Synchronize()
        {
            var masterEntries = _master.GetAll();
            var slaveEntries = _slave.GetAll();

            var idsForDeletion =
                slaveEntries.Select(e => e.Id).Except(
                    masterEntries.Where(me => _map.ContainsKey(_master, me.Status)).Select(e => e.Id));
 
            foreach (var id in idsForDeletion)
            {
                var entry = slaveEntries.Where(e=>e.Id==id).Single();
                _slave.Delete(entry);
            }

            var idsForCreation = masterEntries.Select(e => e.Id).Except(slaveEntries.Select(e => e.Id));
            foreach (var id in idsForCreation)
            {
                try
                {

                    var entry = masterEntries.Where(e => e.Id == id).Single();

                    SynchronizationEntry slaveEntry;

                    if (TryGetSlaveEntry(entry, out slaveEntry))
                    {
                        _slave.Create(slaveEntry);
                    }
                }
                catch(InvalidOperationException e)
                {
                    throw new InvalidOperationException("Id='"+id+"'",e);
                }
            }
        }

        private bool TryGetSlaveEntry(SynchronizationEntry masterEntry, out SynchronizationEntry slaveEntry)
        {
            slaveEntry = null;

            if(!_map.ContainsKey(_master, masterEntry.Status))
            {
                return false;
            }

            var slaveStatus = _map.GetMappedValue(_master, masterEntry.Status);

            slaveEntry = new SynchronizationEntry(masterEntry.Id, slaveStatus, new Dictionary<string, string>());

            return true;
        }
    }
}