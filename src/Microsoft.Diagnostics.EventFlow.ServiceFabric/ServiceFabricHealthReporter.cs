﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using System.Fabric.Health;

namespace Microsoft.Diagnostics.EventFlow.ServiceFabric
{
    public class ServiceFabricHealthReporter : IHealthReporter
    {
        private FabricClient fabricClient;
        private Uri applicatioName;
        private string serviceManifestName;
        private string nodeName;
        private string entityIdentifier;

        public ServiceFabricHealthReporter(string entityIdentifier)
        {
            if (string.IsNullOrWhiteSpace(entityIdentifier))
            {
                throw new ArgumentException("entityIdentifier cannot be null or empty", "entityIdentifier");
            }
            this.entityIdentifier = entityIdentifier;

            this.fabricClient = new FabricClient(
                new FabricClientSettings()
                {
                    HealthReportSendInterval = TimeSpan.FromSeconds(5)
                }
                );

            CodePackageActivationContext activationContext = FabricRuntime.GetActivationContext();
            this.applicatioName = new Uri(activationContext.ApplicationName);
            this.serviceManifestName = activationContext.GetServiceManifestName();
            NodeContext nodeContext = FabricRuntime.GetNodeContext();
            this.nodeName = nodeContext.NodeName;
        }

        public void ReportHealthy(string description = "Healthy", string context = null)
        {
            ReportMessage(HealthState.Ok, description);
        }

        public void ReportProblem(string description, string context = null)
        {
            ReportMessage(HealthState.Error, description);
        }

        public void ReportWarning(string description, string context = null)
        {
            ReportMessage(HealthState.Warning, description);
        }

        private void ReportMessage(HealthState healthState, string description)
        {
            HealthInformation healthInformation = new HealthInformation(this.entityIdentifier, "Connectivity", healthState);
            healthInformation.Description = description;

            DeployedServicePackageHealthReport healthReport = new DeployedServicePackageHealthReport(
                this.applicatioName,
                this.serviceManifestName,
                this.nodeName,
                healthInformation);

            try
            {
                this.fabricClient.HealthManager.ReportHealth(healthReport);
            }
            catch (FabricException e)
            {
                // A stale report exception indicates a newer report was submitted for the same entity.
                // Because we are reporting health for deployed service package, this can happen if multiple instances or replicas
                // of the same service are deployed on the same cluster node. We could report on service instances/replicas instead,
                // and that would make health reports unique, but would also significantly complicate EventFlow setup, 
                // so we just suppress the error instead.
                if (e.ErrorCode != FabricErrorCode.FabricHealthStaleReport)
                {
                    throw;
                }
            }
        }

        public void Dispose()
        {
            // Recycle resource when necessary.
        }
    }
}