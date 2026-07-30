using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Culture = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTranslations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTranslations_EntityType_EntityId_Field_Culture",
                table: "ContentTranslations",
                columns: new[] { "EntityType", "EntityId", "Field", "Culture" },
                unique: true);

            MoveSeededTextIntoTranslations(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Put the Uzbek category names back in the column they came from before the table holding them
            // disappears, so rolling back does not silently leave the catalog in English.
            migrationBuilder.Sql("""
                UPDATE "Categories" c
                SET "Name" = t."Value", "UpdatedAt" = now()
                FROM "ContentTranslations" t
                WHERE t."EntityType" = 'Category'
                  AND t."EntityId" = c."Id"
                  AND t."Field" = 'Name'
                  AND t."Culture" = 'uz';
                """);

            migrationBuilder.DropTable(
                name: "ContentTranslations");
        }

        /// <summary>Rehomes the text that shipped in the seed data. Categories were seeded in Uzbek while every
        /// other canonical column is English, so their current value is moved into a <c>uz</c> translation and
        /// the column is rewritten in English; banners and the home-intro block are already English and only
        /// gain translations. Every statement is scoped to the known seeded rows and is a no-op on a fresh
        /// database (the tables are still empty at this point) or on a second run.</summary>
        private static void MoveSeededTextIntoTranslations(MigrationBuilder migrationBuilder)
        {
            const string categoryMap = """
                WITH m(slug, en, ru) AS (VALUES
                    ('computers',   'Computers',   'Компьютеры'),
                    ('laptops',     'Laptops',     'Ноутбуки'),
                    ('accessories', 'Accessories', 'Аксессуары'),
                    ('mice',        'Mice',        'Мыши'),
                    ('memory',      'Memory',      'Память'))
                """;

            // Uzbek first: it is read out of the column that the UPDATE below overwrites.
            migrationBuilder.Sql($"""
                {categoryMap}
                INSERT INTO "ContentTranslations"
                    ("Id", "EntityType", "EntityId", "Field", "Culture", "Value", "CreatedAt")
                SELECT gen_random_uuid(), 'Category', c."Id", 'Name', 'uz', c."Name", now()
                FROM "Categories" c
                JOIN m ON m.slug = c."Slug"
                WHERE c."Name" <> m.en
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql($"""
                {categoryMap}
                INSERT INTO "ContentTranslations"
                    ("Id", "EntityType", "EntityId", "Field", "Culture", "Value", "CreatedAt")
                SELECT gen_random_uuid(), 'Category', c."Id", 'Name', 'ru', m.ru, now()
                FROM "Categories" c
                JOIN m ON m.slug = c."Slug"
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql($"""
                {categoryMap}
                UPDATE "Categories" c
                SET "Name" = m.en, "UpdatedAt" = now()
                FROM m
                WHERE m.slug = c."Slug" AND c."Name" <> m.en;
                """);

            migrationBuilder.Sql("""
                WITH m(title, ru_title, uz_title, ru_sub, uz_sub) AS (VALUES
                    ('Back-to-work deals',
                     'Скидки к началу рабочего сезона',
                     'Ish mavsumi chegirmalari',
                     'Скидки на ноутбуки, комплектующие и аксессуары на этой неделе.',
                     'Shu hafta noutbuklar, komplektuvchilar va aksessuarlarga chegirmalar.'),
                    ('Genuine brands, fast delivery',
                     'Оригинальные бренды, быстрая доставка',
                     'Original brendlar, tez yetkazib berish',
                     'ASUS · Logitech · Kingston и другие.',
                     'ASUS · Logitech · Kingston va boshqalar.'))
                INSERT INTO "ContentTranslations"
                    ("Id", "EntityType", "EntityId", "Field", "Culture", "Value", "CreatedAt")
                SELECT gen_random_uuid(), 'Banner', b."Id", f.field, f.culture, f.value, now()
                FROM "Banners" b
                JOIN m ON m.title = b."Title"
                CROSS JOIN LATERAL (VALUES
                    ('Title',    'ru', m.ru_title),
                    ('Title',    'uz', m.uz_title),
                    ('Subtitle', 'ru', m.ru_sub),
                    ('Subtitle', 'uz', m.uz_sub)) AS f(field, culture, value)
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "ContentTranslations"
                    ("Id", "EntityType", "EntityId", "Field", "Culture", "Value", "CreatedAt")
                SELECT gen_random_uuid(), 'CmsBlock', b."Id", f.field, f.culture, f.value, now()
                FROM "CmsBlocks" b
                CROSS JOIN LATERAL (VALUES
                    ('Title', 'ru', 'Добро пожаловать в PCMarket'),
                    ('Title', 'uz', 'PCMarket''ga xush kelibsiz'),
                    ('Body',  'ru', 'Магазин ПК и электроники в Узбекистане — оригинальная техника, оплата Click/Payme/Uzcard/Humo и наличными при получении.'),
                    ('Body',  'uz', 'O‘zbekistondagi kompyuter va elektronika do‘koni — original texnika, Click/Payme/Uzcard/Humo orqali va yetkazib berishda naqd to‘lov.')) AS f(field, culture, value)
                WHERE b."Key" = 'home-intro'
                ON CONFLICT DO NOTHING;
                """);
        }
    }
}
