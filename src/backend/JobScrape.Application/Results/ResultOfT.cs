namespace JobScrape.Application.Results
{
    /// <summary>
    /// Success/failure outcome for use cases with a return value. Success
    /// carries exactly one value; failure carries exactly one
    /// <see cref="ErrorInfo"/>. Instances come from the <see cref="Result"/>
    /// factory hub so no static members sit on this generic type.
    /// </summary>
    public sealed class Result<T>
    {
        private readonly T? _value;
        private readonly ErrorInfo? _error;

        internal Result(T? value, ErrorInfo? error, bool isSuccess)
        {
            _value = value;
            _error = error;
            IsSuccess = isSuccess;
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
    }
}
