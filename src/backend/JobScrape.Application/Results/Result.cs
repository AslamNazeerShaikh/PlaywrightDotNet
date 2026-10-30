namespace JobScrape.Application.Results
{
    /// <summary>
    /// Success/failure outcome for use cases with no return value, and the
    /// factory hub for <see cref="Result{T}"/>.
    /// </summary>
    public sealed class Result
    {
        private readonly ErrorInfo? _error;

        private Result(bool isSuccess, ErrorInfo? error)
        {
            IsSuccess = isSuccess;
            _error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public ErrorInfo Error
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

        public static Result Fail(ErrorInfo error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new Result(false, error);
        }

        public static Result<T> Ok<T>(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new Result<T>(value, null, true);
        }

        public static Result<T> Fail<T>(ErrorInfo error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new Result<T>(default, error, false);
        }
    }
}
