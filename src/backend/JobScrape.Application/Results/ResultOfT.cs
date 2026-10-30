namespace JobScrape.Application.Results
{
    /// <summary>
    /// Success/failure outcome for use cases with a return value. Success
    /// carries exactly one value; failure carries exactly one
    /// <see cref="ErrorInfo"/>. Instances come from the <see cref="Result"/>
    /// factory hub so no static members sit on this generic type.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    public sealed class Result<T>
    {
        // Instance state only; this type has no static members by design
        // (CA1000). All construction goes through the Result factory hub.
        private readonly T? _value;
        private readonly ErrorInfo? _error;

        /// <summary>
        /// Initializes a new instance. Internal so every outcome is created
        /// through the <see cref="Result"/> factories, which validate arguments
        /// and keep the success/error invariant in one place.
        /// </summary>
        /// <param name="value">The success value; required when <paramref name="isSuccess"/> is true.</param>
        /// <param name="error">The failure error; required when <paramref name="isSuccess"/> is false.</param>
        /// <param name="isSuccess">Whether this outcome represents success.</param>
        internal Result(T? value, ErrorInfo? error, bool isSuccess)
        {
            _value = value;
            _error = error;
            IsSuccess = isSuccess;
        }

        /// <summary>
        /// Gets a value indicating whether the operation succeeded.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether the operation failed.
        /// This is always the negation of <see cref="IsSuccess"/>.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Gets the success value.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when accessed on a failure. Check <see cref="IsSuccess"/> first.
        /// </exception>
        public T Value
        {
            get
            {
                if (IsFailure)
                {
                    throw new InvalidOperationException("Value is only available on success.");
                }

                // _value is non-null on the success path: Ok<T> rejects null.
                return _value!;
            }
        }

        /// <summary>
        /// Gets the failure error.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when accessed on a success. Check <see cref="IsFailure"/> first.
        /// </exception>
        public ErrorInfo Error
        {
            get
            {
                if (IsSuccess)
                {
                    throw new InvalidOperationException("Error is only available on failure.");
                }

                // _error is non-null on the failure path: Fail<T> rejects null.
                return _error!;
            }
        }
    }
}
