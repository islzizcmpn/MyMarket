namespace PcMarket.Web.Services;

/// <summary>
/// The seeded Phase 17 news/promotions feed. Three dummy articles held in memory — there is no
/// `StockArticle` entity, no table and no admin surface behind this yet.
/// <para>
/// Deliberately shaped like <see cref="HomeImages"/>: a singleton over a fixed set, resolving its
/// artwork through that same service so a banner whose file is missing falls back rather than
/// emitting a broken image. When these articles become real content, this class is the seam —
/// swap the list for a `ContentApiClient` call (the Phase 6 `CmsBlock` path is the natural home)
/// and neither page needs to change.
/// </para>
/// <para>
/// <b>Copy is seeded in Russian only</b>, the storefront's default culture. The surrounding page
/// chrome is fully localized under <c>Stock.*</c>, but an article's own title and body are content,
/// not UI strings, so they do not belong in the resx files — a real feed would carry a translation
/// per article. Under UZ or EN the chrome switches and the article text stays Russian.
/// </para>
/// </summary>
public sealed class StockArticles(HomeImages images)
{
    /// <summary>Newest first — the order the list page renders and the order the cards read in.</summary>
    private static readonly StockArticle[] Seed =
    [
        new(
            Slug: "sborka-igrovogo-pk-so-skidkoy",
            Title: "Сборка игрового ПК со скидкой до 15%",
            Excerpt: "Собираем игровой компьютер под ваш бюджет и дарим скидку на сборку — " +
                     "предложение действует до конца месяца.",
            Body:
            [
                "До конца месяца мы собираем игровые компьютеры со скидкой до 15% на стоимость " +
                "сборки. Предложение распространяется на все конфигурации, собранные из " +
                "комплектующих, приобретённых в PC Market.",
                "Наши инженеры подберут связку процессора, видеокарты и оперативной памяти под " +
                "ваши задачи — от киберспортивных дисциплин на высоком FPS до монтажа видео и " +
                "работы с 3D. Мы учитываем не только производительность, но и тепловой режим, " +
                "уровень шума и запас по питанию, чтобы система оставалась стабильной под " +
                "долгой нагрузкой.",
                "Каждая сборка проходит стресс-тест и проверку температур перед выдачей. Вы " +
                "получаете готовый компьютер с установленной операционной системой, актуальными " +
                "драйверами и настроенным профилем охлаждения.",
                "Обратите внимание: размер скидки зависит от итоговой конфигурации. Точную " +
                "стоимость подскажет менеджер после согласования списка комплектующих.",
            ],
            PublishedOn: new DateOnly(2026, 7, 28),
            ImageFile: "promo-gaming.jpg"),
        new(
            Slug: "novye-videokarty-v-nalichii",
            Title: "Новые видеокарты уже в наличии",
            Excerpt: "Пополнили склад видеокартами текущего поколения — от решений для " +
                     "Full HD до карт для 4K и работы с нейросетями.",
            Body:
            [
                "На склад поступила новая партия видеокарт текущего поколения. В наличии " +
                "решения под любой сценарий: от компактных карт для Full HD до старших моделей " +
                "для 4K, трассировки лучей и локального запуска нейросетей.",
                "Все карты — официальные поставки с полной гарантией производителя. Мы проверяем " +
                "каждую позицию перед отправкой на витрину: тестируем под нагрузкой, снимаем " +
                "температуры и убеждаемся, что система охлаждения работает штатно.",
                "Если вы не уверены, какая карта подойдёт под ваш монитор и процессор, напишите " +
                "нам — подберём вариант без переплаты за производительность, которую вы не " +
                "сможете использовать.",
            ],
            PublishedOn: new DateOnly(2026, 7, 14),
            ImageFile: "feat-gpu-1.jpg"),
        new(
            Slug: "besplatnaya-diagnostika",
            Title: "Бесплатная диагностика в сервисном центре",
            Excerpt: "Приносите технику на диагностику — определим причину неисправности " +
                     "и рассчитаем стоимость ремонта бесплатно.",
            Body:
            [
                "Наши сервисные партнёры проводят диагностику компьютерной техники бесплатно. " +
                "Вы приносите устройство, мы определяем причину неисправности и называем точную " +
                "стоимость ремонта до того, как начнём работу.",
                "Диагностика занимает от одного до трёх рабочих дней в зависимости от сложности " +
                "случая. Если ремонт вам не подойдёт по цене или срокам, вы забираете технику " +
                "без каких-либо обязательств — платить за саму диагностику не нужно.",
                "Услуга распространяется на настольные компьютеры, ноутбуки, мониторы и " +
                "периферию — независимо от того, где именно техника была куплена.",
            ],
            PublishedOn: new DateOnly(2026, 6, 30),
            ImageFile: "promo-setup.jpg"),
    ];

    public IReadOnlyList<StockArticle> All => Seed;

    /// <summary>The article at <paramref name="slug"/>, or <see langword="null"/> so the detail page
    /// can render its own not-found state rather than throwing.</summary>
    public StockArticle? Find(string? slug) =>
        Seed.FirstOrDefault(article =>
            string.Equals(article.Slug, slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>Web path for an article's banner, or <see langword="null"/> when the file is absent.</summary>
    public string? ImagePath(StockArticle article) => images.Path(article.ImageFile);
}
