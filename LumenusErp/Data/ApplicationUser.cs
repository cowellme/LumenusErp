using Microsoft.AspNetCore.Identity;

namespace LumenusErp.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public int? SpaceId { get; set; }
        public List<Space>? Spaces { get; set; }
    }

    public class Space
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Token { get; set; }
        public bool IsMark { get; set; } = false;
        public bool IsTelegram { get; set; } = false;
        public bool IsSpace { get; set; } = true;
        public List<ErpGroup>? Groups { get; set; }
    }

    public class ErpGroup
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? NameOfLot { get; set; }
        public int LotCount { get; set; }
        public bool IsActive { get; set; } = true;
    }

}
