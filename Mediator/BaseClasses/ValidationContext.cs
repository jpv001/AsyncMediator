using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AsyncMediator
{
    public class ValidationContext
    {
        public List<ValidationResult> ValidationResults { get; set; }

        public ValidationContext()
        {
            ValidationResults = new List<ValidationResult>();
        }

        public void AddError(string memberName, string errorMessage)
        {
            ValidationResults.Add(new ValidationResult(errorMessage, new[] { memberName }));
        }

        public void AddErrors(IEnumerable<ValidationResult> errors)
        {
            ValidationResults.AddRange(errors);
        }
    }
}
