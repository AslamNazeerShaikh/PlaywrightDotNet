namespace JobScrape.Application.Results
{
    /// <summary>
    /// Success/failure outcome for use cases with no return value, and the
    /// factory hub for <see cref="Result{T}"/>.
    /// <para>
    /// The hub holds the static factories because static members are not
    /// allowed on the generic <see cref="Result{T}"/> type. The factories are
    /// stateless pure functions: each call allocates a fresh outcome object
    /// and retains nothing, so concurrent callers never share state.
    /// </para>
    /// </summary>
    public sealed class Result
    {
        // Instance state only; no static fields anywhere in this type, so
        // nothing is shared between callers across the application lifetime.
        private readonly ErrorInfo? _error;

        /// <summary>
        /// Initializes a new void outcome. Private so every outcome flows
        /// through the static factories, which validate arguments.
        /// </summary>
        /// <param name="isSuccess">Whether this outcome represents success.</param>
        /// <param name="error">The failure error; required when <paramref name="isSuccess"/> is false.</param>
        private Result(bool isSuccess, ErrorInfo? error)
        {
            IsSuccess = isSuccess;
            _error = error;
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

                // _error is non-null on the failure path: Fail rejects null.
                return _error!;
            }
        }

        /// <summary>
        /// Creates a successful void outcome.
        /// </summary>
        /// <returns>A successful <see cref="Result"/>.</returns>
        public static Result Ok()
        {
            return new Result(true, null);
        }

        /// <summary>
        /// Creates a failed void outcome.
        /// </summary>
        /// <param name="error">The typed failure. Must not be null.</param>
        /// <returns>A failed <see cref="Result"/> carrying <paramref name="error"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
        public static Result Fail(ErrorInfo error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new Result(false, error);
        }

        /// <summary>
        /// Creates a successful outcome carrying <paramref name="value"/>.
        /// Synchronous by design: wrapping a value performs no I/O, so there
        /// is nothing to await.
        /// </summary>
        /// <typeparam name="T">The type of the success value.</typeparam>
        /// <param name="value">The success value. Must not be null.</param>
        /// <returns>A successful <see cref="Result{T}"/> carrying <paramref name="value"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
        public static Result<T> Ok<T>(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new Result<T>(value, null, true);
        }

        /// <summary>
        /// Creates a failed outcome carrying <paramref name="error"/>.
        /// Synchronous by design: wrapping an error performs no I/O, so there
        /// is nothing to await.
        /// </summary>
        /// <typeparam name="T">The type the success value would have had.</typeparam>
        /// <param name="error">The typed failure. Must not be null.</param>
        /// <returns>A failed <see cref="Result{T}"/> carrying <paramref name="error"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
        public static Result<T> Fail<T>(ErrorInfo error)
        {
            ArgumentNullException.ThrowIfNull(error);

            // default is safe here: Value throws on the failure path, so the
            // placeholder is never observed.
            return new Result<T>(default, error, false);
        }
    }
}
