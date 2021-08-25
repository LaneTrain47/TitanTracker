using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TitanTracker.Models
{
    public class Project
    {
        //Primary Key        
        public int Id { get; set; }

        [DisplayName("Company ID")]
        public int? CompanyId { get; set; }

        [DisplayName("Name")]
        public string Name { get; set; }

        [Required]
        [DisplayName("Description")]
        public string Description { get; set; }


        [DisplayName("Start Date")]
        public int TicketId { get; set; }

        [Required]
        [DisplayName("End Date")]
        public string RecipientId { get; set; }

        [Required]
        [DisplayName("Project Priority ID")]
        public string SenderId { get; set; }

        //--Navigation Properties--//
        public virtual Company Company { get; set; }
        public virtual ProjectPriority ProjectPriority { get; set; }
        //public virtual Members Members { get; set; }
        //public virtual Tickets Tickets { get; set; }

    }
}
