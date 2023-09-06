using System.ComponentModel.DataAnnotations;

namespace Bahadır_webForm.Models
{
    public class Information
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string PhoneNumber { get; set; }
        public string Ssn { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }
        public DateTime EntryDate { get; set; }
    }

}
