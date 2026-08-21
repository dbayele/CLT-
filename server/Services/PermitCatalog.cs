using CltPlusPlus.Api.Models;

namespace CltPlusPlus.Api.Services;

public static class PermitCatalog
{
    private const string CityPermits = "https://www.charlottenc.gov/Services/Permits";
    private const string Development = "https://www.charlottenc.gov/Growth-and-Development/Getting-Started-on-Your-Project";
    private const string Zoning = "https://www.charlottenc.gov/Growth-and-Development/Planning-and-Development/Zoning/Permitting";

    public static readonly IReadOnlyList<PermitDefinition> All = new List<PermitDefinition>
    {
        new("zoning-use","Zoning","Planning, Design & Development","Zoning Use Permit","Zoning approval for uses such as home-based businesses, mobile vendors, temporary uses, accessory structures, and changes of use.",true,Zoning),
        new("sign-permit","Zoning","Planning, Design & Development","Sign Permit","Application intake for permanent or temporary signs requiring City zoning review.",true,Development),
        new("zoning-verification","Zoning","Planning, Design & Development","Zoning Verification Letter","Request formal zoning verification for a property or proposed business use.",true,Development),
        new("abc-zoning-inspection","Zoning","Planning / Fire / Building","ABC Inspection / Zoning Compliance","Collect the business and premises information needed for local zoning, fire, and building portions of an ABC inspection process.",true,Zoning),
        new("ldirl","Land Development","CLT Development Center","Individual Residential Lot / LDIRL","City land-development review used alongside applicable Mecklenburg County building permits.",true,Development),
        new("commercial-plan-review","Land Development","CLT Development Center","Commercial Plan Review","Commercial site/development review involving applicable City review agencies.",true,Development),
        new("commercial-zoning-review","Zoning","CLT Development Center","Commercial Zoning Only Review","Commercial zoning review for projects that do not require the full land-development review path.",true,Development),
        new("grading","Land Development","CLT Development Center","Grading Permit / Review","Application intake for qualifying land disturbance and grading work.",true,Development),
        new("driveway","Transportation","Charlotte DOT / Development Center","Driveway Permit","Driveway connection and related transportation review for development projects.",true,Development),
        new("right-of-way-use","Transportation","Charlotte DOT","Right-of-Way Use Permit","Request use or occupancy of City right-of-way for qualifying work or activity.",true,CityPermits),
        new("utility-row","Transportation","Charlotte DOT","Utility Right-of-Way Permit","Utility work and right-of-way management application intake.",true,CityPermits),
        new("encroachment","Transportation","Charlotte DOT","Encroachment Agreement","Request review for construction or installation within City right-of-way.",true,CityPermits),
        new("right-of-way-abandonment","Transportation","Charlotte DOT","Right-of-Way Abandonment","Application intake for approved right-of-way abandonment administration.",true,CityPermits),
        new("oversized-load","Transportation","Charlotte DOT","Oversized Load / House Moving Permit","Request approval to move oversized loads or structures on City streets.",true,CityPermits),
        new("fence-wall-certificate","Transportation","Charlotte DOT","Fence and Wall Certificate","No-cost certificate workflow for fences or walls along City streets where applicable.",true,CityPermits),
        new("residential-parking","Parking","Charlotte DOT / Park It","Residential Parking Permit","Residential parking permit application intake for eligible areas.",true,CityPermits),
        new("valet-parking","Parking","Charlotte DOT","Valet Parking Permit","Business application intake for valet operations in the public realm.",true,CityPermits),
        new("street-vendor","Business","Charlotte DOT","Street Vendor Program Permit","Application intake for qualifying street-vendor programs and locations.",true,CityPermits),
        new("newsrack","Business","Charlotte DOT","Newsrack Permit","Annual newsrack/bin permit application intake.",true,CityPermits),
        new("event-street","Events","Charlotte DOT / Special Events","Street Closure / Event Permit","Application intake for events affecting streets, closures, or detours.",true,CityPermits),
        new("public-assembly","Events","City of Charlotte","Public Assembly / Parade Permit","Application intake for qualifying public assemblies and parades.",true,CityPermits),
        new("amplified-sound","Events","CMPD","Amplified Sound Permit","Application intake for amplified sound when a separate permit is required.",true,CityPermits),
        new("private-traffic-control","Events","CMPD","Private Traffic Control","Request off-duty police traffic-control staffing for an event or private activity.",true,CityPermits),
        new("fire-prevention","Fire","Charlotte Fire","Fire Prevention Permit","General Fire Prevention permit/application intake for regulated occupancies, systems, or activities.",true,CityPermits),
        new("fire-system-impairment","Fire","Charlotte Fire","Fire Protection System Impairment Notice","Notify Fire Prevention of an impairment to a fire alarm, sprinkler, suppression, or related protection system.",true,CityPermits),
        new("temporary-tent","Zoning / Fire","Planning / Charlotte Fire","Tent / Temporary Structure Permit","Coordinate zoning/fire review for qualifying tents and temporary structures.",true,Zoning),
        new("mobile-food-vendor","Business","Planning / Charlotte Fire","Mobile Food Vendor Zoning / Fire Review","Application intake for mobile food vendor zoning and applicable fire-safety review.",true,Zoning),
        new("home-based-business","Business","Planning, Design & Development","Home-Based Business Zoning Approval","Zoning-use application intake for a business operated from a residence.",true,Zoning),
        new("change-of-use","Zoning","Planning / Mecklenburg County","Change of Use Review","Application intake for a change in how an existing building or tenant space is used.",true,Zoning)
    };
}
