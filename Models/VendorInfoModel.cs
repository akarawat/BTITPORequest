using System.ComponentModel.DataAnnotations;

namespace BTITPORequest.Models
{
    public class VendorInfoModel
    {
        public int    VendorId      { get; set; }

        [Required(ErrorMessage = "กรุณาระบุชื่อผู้ติดต่อ")]
        [Display(Name = "Contact Person")]
        public string VendorAttn    { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณาระบุชื่อบริษัท")]
        [Display(Name = "Company Name")]
        public string VendorCompany { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณาระบุที่อยู่")]
        [Display(Name = "Address")]
        public string VendorAddress { get; set; } = string.Empty;

        [Display(Name = "Tel No.")]
        public string VendorTel     { get; set; } = string.Empty;

        [Display(Name = "Fax No.")]
        public string VendorFax     { get; set; } = string.Empty;

        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "รูปแบบ Email ไม่ถูกต้อง")]
        public string VendorEmail   { get; set; } = string.Empty;
    }

    public class VendorIndexViewModel
    {
        public List<VendorInfoModel> Vendors    { get; set; } = new();
        public string?               SearchText { get; set; }
        public int                   TotalCount => Vendors.Count;
    }
}
