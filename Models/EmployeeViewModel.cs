using System;
using System.ComponentModel.DataAnnotations;

namespace TechVision.Models
{
    public class EmployeeViewModel
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }

        public string Address { get; set; }

        public string CompanyName { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Salary cannot be negative")]
        public decimal Salary { get; set; }

        public string MaritalStatus { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        public string Gender { get; set; }

        [Phone]
        public string Mobile { get; set; }
    }
}