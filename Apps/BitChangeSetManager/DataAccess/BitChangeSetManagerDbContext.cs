﻿using Bit.Data.EntityFramework.Implementations;
using BitChangeSetManager.Model;
using Foundation.Core.Contracts;
using Foundation.DataAccess.Contracts;
using System.Data.Common;
using System.Data.Entity;

namespace BitChangeSetManager.DataAccess
{
    public class BitChangeSetManagerDbContext : DefaultDbContext
    {
        public BitChangeSetManagerDbContext(DbConnection existingConnection)
            : base(existingConnection, contextOwnsConnection: true)
        {

        }

        public BitChangeSetManagerDbContext(IAppEnvironmentProvider appEnvironmentProvider, IDbConnectionProvider dbConnectionProvider)
            : base(appEnvironmentProvider.GetActiveAppEnvironment().GetConfig<string>("BitChangeSetManagerDbConnectionString"), dbConnectionProvider)
        {

        }

        public virtual DbSet<ChangeSet> ChangeSets { get; set; }

        public virtual DbSet<Customer> Customers { get; set; }

        public virtual DbSet<Delivery> Deliveries { get; set; }

        public virtual DbSet<User> Users { get; set; }
    }
}