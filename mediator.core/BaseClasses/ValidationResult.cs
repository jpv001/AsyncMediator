using System.Collections.Generic;

namespace AsyncMediator
{
    public class ValidationResult
    {
        public ValidationResult(string memberName, string errorMessage)
        {
            MemberName = memberName;
            ErrorMessage = errorMessage;
        }

        public string MemberName { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Adds a validation error.
        /// </summary>
        /// <param name="memberName">The member that has caused validation to fail.</param>
        /// <param name="errorMessage">The validation error message.</param>
        public void AddError(string errorMessage, string memberName)
        {
        	MemberName = memberName;
        	ErrorMessage = errorMessage;
        }
    }
}
