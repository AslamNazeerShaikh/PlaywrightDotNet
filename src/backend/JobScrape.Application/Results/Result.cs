namespace JobScrape.Application.Results
{
    /// <summary>
    /// Success/failure outcome for use cases with no return value.
    /// </summary>
    public sealed class Result
    {
        private readonly Error? _error;

        private Result(bool isSuccess, Error? error)
        {
            IsSuccess = isSuccess;
            _error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public Error Error
        {
            get
            {
                if (IsSuccess)
                {
                    throw new InvalidOperationException("Error is only available on failure.");
                }

                return _error!;
            }
        }

        public static Result Ok()
        {
            return new Result(true, null);
        }

        public static Result Fail(Error error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new Result(false, error);
        }
    }
}
