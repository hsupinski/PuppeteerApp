namespace PuppeteerApp
{
    public class ErrorMessageService
    {
        List<string> _errors;

        public ErrorMessageService()
        {
            _errors = new List<string>();
        }

        public void AddError(string error)
        {
            _errors.Add(error);
        }

        public string getErrorsAsString()
        {
            var connectedErrors = string.Join("\n", _errors.Select(x => x + " "));

            ClearErrors();
            return connectedErrors;
        }

        public void ClearErrors()
        {
            _errors.Clear();
        }

        public bool HasErrors()
        {
            return _errors.Count > 0;
        }
    }
}
