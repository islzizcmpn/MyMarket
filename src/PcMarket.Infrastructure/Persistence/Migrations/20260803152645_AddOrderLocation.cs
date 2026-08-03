using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcMarket.Infrastructure.Persistence.Migrations
{
    /// <summary>Deliberately empty. Orders.ShippingAddress is an owned entity mapped with ToJson(), so adding
    /// Latitude and Longitude changes the shape of a JSON document rather than the table - there is no column
    /// to add. The migration is kept rather than deleted because it carries the model snapshot forward; drop
    /// it and the next migration would try to account for this change again.</summary>
    public partial class AddOrderLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
