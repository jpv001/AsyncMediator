namespace AsyncMediator
{
    /// <summary>
    /// A generic version of the <see cref="CommandWorkflowResult"/> class, including a workflow result object.
    /// </summary>
    /// <typeparam name="T">The type of workflow result</typeparam>
    public class CommandWorkflowResult<T> : CommandWorkflowResult
    {
        /// <summary>
        /// The workflow result.
        /// </summary>
        public T Result { get; set; }

        /// <summary>
        /// Default constructor.
        /// Creates a new <see cref="CommandWorkflowResult{T}"/>.
        /// </summary>
        public CommandWorkflowResult()
        {
        }

        /// <summary>
        /// Public constructor.
        /// Creates a new <see cref="CommandWorkflowResult{T}"/> with a Result.
        /// </summary>
        /// <param name="result">The result instance.</param>
        public CommandWorkflowResult(T result)
        {
            Result = result;
        }

        /// <summary>
        /// Determines whether the specified object is equal to this object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if they are equal; false if not.</returns>
        public override bool Equals(object obj)
        {
            var other = obj as CommandWorkflowResult<T>;
            return other != null && ResultEquals(other.Result) && base.Equals(other);
        }

        /// <summary>
        /// Determines whether the Result object is equal to the other Result object.
        /// </summary>
        /// <param name="otherResult">The other result.</param>
        /// <returns>True if they are equal; false if not.</returns>
        protected bool ResultEquals(T otherResult)
        {
            return (Result == null) ? (otherResult == null) : Result.Equals(otherResult);
        }
    }
}
