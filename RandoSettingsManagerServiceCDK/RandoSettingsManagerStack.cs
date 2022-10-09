using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace RandoSettingsManagerServiceCDK
{
    public class RandoSettingsManagerStack : Stack
    {
        internal RandoSettingsManagerStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            Table quickShareSettingsStore = new Table(this, "QuickShareSettingsStore", new TableProps
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
                "dotnet lambda package -o bin/Publish/package.zip",
                "unzip -o -d /asset-output bin/Publish/package.zip"
            };

            Function createSettings = new(this, "SettingsManagerLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_6,
                Code = Code.FromAsset("../RandoSettingsManagerService", new Amazon.CDK.AWS.S3.Assets.AssetOptions
                {
                    Bundling = new BundlingOptions
                    {
                        Image = Runtime.DOTNET_6.BundlingImage,
                        Command = new[]
                        {
                            "bash", "-c", string.Join(" && ", bundlingCommands)
                        }
                    }
                }),
                Handler = "RandoSettingsManagerService::RandoSettingsManagerService.Handlers::FunctionHandler",
                FunctionName = "ManageSettings",
                Timeout = Duration.Seconds(30),
            });

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
