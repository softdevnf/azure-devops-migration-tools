﻿using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TfsWitMigrator.Core.ComponentContext
{
   public interface IFieldMap
    {
        void Execute(WorkItem source, WorkItem target);
    }
}
