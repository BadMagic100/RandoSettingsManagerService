using Amazon.CDK;
using RandoSettingsManagerServiceCDK;

namespace RandoSettingsManagerCDK
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            App app = new();
            new RandoSettingsManagerStack(app, "RandoSettingsManager", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
                }
            });
            app.Synth();
        }
    }
}