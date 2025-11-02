using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Constructs;

namespace RandoSettingsManagerServiceCDK
{
    public class RandoSettingsManagerStack : Stack
    {
        internal RandoSettingsManagerStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            Table quickShareSettingsStore = new(this, "QuickShareSettingsStore", new TableProps
            {
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
                {
                    Name = "ID",
                    Type = AttributeType.STRING
                },
                TimeToLiveAttribute = "ExpiryTimestamp",
                BillingMode = BillingMode.PAY_PER_REQUEST,
                TableName = "QuickShareSettingsStore",
            });

            IEnumerable<string> bundlingCommands = new[]
            {
                "cd /asset-input",
                "export DOTNET_CLI_HOME=\"/tmp/DOTNET_CLI_HOME\"",
                "export PATH=\"$PATH:/tmp/DOTNET_CLI_HOME/.dotnet/tools\"",
                "dotnet tool install -g Amazon.Lambda.Tools",
                "dotnet lambda package -o bin/Publish/package.zip -c Release",
                "unzip -o -d /asset-output bin/Publish/package.zip"
            };

            LogGroup settingsManagerLogGroup = new(this, "SettingsManagerLogGroup", new LogGroupProps
            {
                LogGroupName = "SettingsManager-LogGroup",
                Retention = RetentionDays.THREE_MONTHS,
            });
            Function createSettings = new(this, "SettingsManagerLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Code = Code.FromAsset("../RandoSettingsManagerService", new Amazon.CDK.AWS.S3.Assets.AssetOptions
                {
                    Bundling = new BundlingOptions
                    {
                        Image = Runtime.DOTNET_8.BundlingImage,
                        Command =
                        [
                            "bash", "-c", string.Join(" && ", bundlingCommands)
                        ]
                    }
                }),
                Handler = "RandoSettingsManagerService::RandoSettingsManagerService.Handlers::FunctionHandler",
                FunctionName = "ManageSettings",
                Timeout = Duration.Seconds(30),
                LogGroup = settingsManagerLogGroup,
            });

            Topic alarmTopic = new(this, "AlarmTopic", new TopicProps
            {
                DisplayName = "RandoSettingsManagerService Alarms",
                TopicName = "RSMAlarms"
            });

            Metric _5xxCount = createSettings.Metric("Url5xxCount", new MetricOptions
            {
                Statistic = "Sum",
                Period = Duration.Minutes(1)
            });
            Alarm _5xxAlarm = new(this, "ServerErrors", new AlarmProps
            {
                AlarmName = "RandoSettingsManagerService.ManageSettings.5xxErrors",
                AlarmDescription = "Alarms when the function URL returns any 5xx errors for any 2 out of 5 minutes.",
                Metric = _5xxCount,
                Threshold = 1,
                DatapointsToAlarm = 2,
                EvaluationPeriods = 5,
                TreatMissingData = TreatMissingData.NOT_BREACHING,
                ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            });
            _5xxAlarm.AddAlarmAction(new SnsAction(alarmTopic));

            // calling createSettings.AddFunctionURL is a create-only operation, which is a problem because it fails subsequent deployments. do it by hand.
            new FunctionUrl(this, "SettingsLambdaURL", new FunctionUrlProps
            {
                AuthType = FunctionUrlAuthType.NONE,
                Function = createSettings
            });

            quickShareSettingsStore.GrantReadWriteData(createSettings);
        }
    }
}
