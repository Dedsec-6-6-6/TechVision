using System;

namespace TechVision.Models
{
    public class UserViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public string Address { get; set; }

        public DateTime? DOB { get; set; }
    }
}
