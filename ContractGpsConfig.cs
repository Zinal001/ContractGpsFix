namespace ContractGpsFix
{
    public class ContractGpsConfig
    {
        public string CommandPrefix = "/cgps";
        public int MissingScansBeforeCleanup = 3;
        public NamingScheme NamingScheme = NamingScheme.Letter;
        public bool Verbose = false;
    }
}