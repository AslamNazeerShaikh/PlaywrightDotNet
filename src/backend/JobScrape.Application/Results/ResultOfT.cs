namespace JobScrape.Application.Results
{
    // CA1000: Result<T>.Ok/Fail static factories are the documented
    // application-pattern contract (docs/14-application-patterns.md section 3).
#pragma warning disable CA1000
    /// <summary>
    /// Success/failure outcome for use cases with a return value. Success
    /// carries exactly one value; failure carries exactly one <see cref="Error"/>.
    /// </summary>
    public sealed class Result<T>
    {
        private readonly T? _value;
        private readonly Error? _error;

        private Result(T value)
        {
            IsSuccess = true;
            _value = value;
        }

        private Result(Error error)
        {
            IsSuccess = false;
            _error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public T Value
        {
            get
            {
                if (IsFailure)
                {
                    throw new InvalidOperationException("Value is only available on success.");
                }

                return _value!;
            }
        }

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

        public static Result<T> Ok(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new Result<T>(value);
        }

        public static Result<T> Fail(Error error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new Result<T>(error);
        }
    }
}
