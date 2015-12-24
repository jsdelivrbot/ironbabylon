namespace ModernDev.IronBabylon
{
    public class ParserOptions
    {
        #region Class constructors

        public ParserOptions(string sourceType = "script", bool allowReturnOutsideFunction = false, bool allowImportExportEverywhere = false,
            bool allowSuperOutsideMethod = false, bool strictMode = true)
        {
            SourceType = sourceType;
            AllowReturnOutsideFunction = allowReturnOutsideFunction;
            AllowImportExportEverywhere = allowImportExportEverywhere;
            AllowSuperOutsideMethod = allowSuperOutsideMethod;
            StrictMode = strictMode;

        }

        #endregion

        #region Class fields

        public string SourceType { get; private set; }

        public bool AllowReturnOutsideFunction { get; private set; }

        public bool AllowImportExportEverywhere { get; private set; }

        public bool AllowSuperOutsideMethod { get; private set; }

        public bool StrictMode { get; private set; }

        public static ParserOptions Default => new ParserOptions();

        #endregion
    }
}