namespace ContractGpsFix
{
    public class ContractGpsConfig
    {
        /// <summary>
        /// The prefix for the chat command.
        /// Defaults to /cgps.
        /// </summary>
        public string CommandPrefix = "/cgps";
        
        /// <summary>
        /// The number of times the script has to run to clean up markers that has no connection to any contract.
        /// Defaults to 3.
        /// </summary>
        public int MissingScansBeforeCleanup = 3;
        
        /// <summary>
        /// What naming scheme should be used to generate the markers (A-Z or 1 - 27)?
        /// Valid values: Letter or Number.
        /// Defaults to Letter.
        /// </summary>
        public NamingScheme NamingScheme = NamingScheme.Letter;
        
        /// <summary>
        /// Should verbose chat messages in chat be enabled?
        /// Defaults to false (Not enabled)
        /// </summary>
        public bool Verbose = false;
        
        /// <summary>
        /// Color of the generated markers in HEX format.
        /// Defaults to #FFA500 (Orange).
        /// </summary>
        public string MarkerColor = "#FFA500";
    }
}