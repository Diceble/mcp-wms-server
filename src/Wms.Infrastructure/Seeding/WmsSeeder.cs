using Wms.Domain;
using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Infrastructure.Seeding;

public static class WmsSeeder
{
    /// <summary>Seeds a deterministic bike-parts warehouse with dates relative to today. No-op if data exists.</summary>
    public static void Seed(WmsDbContext db, TimeProvider time)
    {
        if (db.Products.Any())
        {
            return;
        }

        var builder = new SeedBuilder(time.Today());
        builder.Build();

        db.Locations.AddRange(builder.Locations);
        db.Products.AddRange(builder.Products);
        db.Orders.AddRange(builder.Orders);
        db.SaveChanges();
    }

    private sealed class SeedBuilder(DateOnly today)
    {
        private static readonly string[] Customers =
        [
            "Rijwielhandel Van Dijk",
            "Bike Totaal Utrecht",
            "Velo Amsterdam",
            "Wielerhuis Zwolle",
            "Tweewieler Centrum Gouda",
            "Fietsplus Eindhoven",
            "De Fietsfabriek Leiden",
            "Cycle Center Groningen",
            "Rijwielzaak Bakker",
            "Fietsdokter Haarlem",
            "Pedaal & Co",
        ];

        private static readonly (string Sku, string Name, string[] Variants)[] BulkCatalog =
        [
            ("TIR-MAX-MINION", "Maxxis Minion DHF tire", ["27.5", "29"]),
            ("TIR-SCH-MARATHON", "Schwalbe Marathon Plus tire", ["700x35C", "700x40C", "26x2.0"]),
            ("CHN-SHI-HG601", "Shimano HG601 11-speed chain", []),
            ("CHN-SRM-PC1110", "SRAM PC-1110 chain", []),
            ("BRK-SRM-GUIDE", "SRAM Guide brake pads", ["organic", "sintered"]),
            ("CBL-JAG-BRK", "Jagwire brake cable", ["road", "mtb"]),
            ("HLM-ABU-YOUN", "Abus Youn-I helmet", ["S", "M", "L"]),
            ("LGT-BUM-IQX", "Busch+Mueller IQ-X front light", []),
            ("LCK-ABU-GRANIT", "Abus Granit X-Plus lock", []),
            ("SAD-BRK-B17", "Brooks B17 saddle", ["black", "honey", "brown"]),
            ("PDL-CRA-MALLET", "Crankbrothers Mallet E pedals", []),
            ("TUB-CON-RACE", "Continental Race inner tube", ["700x20-25", "700x25-32"]),
            ("GRP-ODI-RUFFIAN", "ODI Ruffian grips", ["black", "red"]),
            ("SPK-DTS-CHAMP", "DT Swiss Champion spoke", ["262mm", "264mm", "266mm"]),
            ("BB-SHI-BB52", "Shimano BB52 bottom bracket", []),
            ("DER-SHI-RD7000", "Shimano 105 rear derailleur", []),
            ("BAR-SPC-TAPE", "Specialized handlebar tape", ["black", "white"]),
            ("PMP-TOP-JOEBLOW", "Topeak JoeBlow floor pump", []),
        ];

        // Includes messy variants of valid codes; they normalize to known statuses.
        private static readonly string[] OpenStatuses = ["NW", "NW", "ALC", "ALC", "PCK", "PSH", "HLD", "BO", "alc", " PCK", "Nw"];

        private readonly Random _rng = new(42);
        private readonly Dictionary<string, Location> _locations = [];
        private readonly Dictionary<string, Product> _products = [];
        private readonly Dictionary<StockLevel, int> _reserved = [];
        private readonly List<string> _bulkSkus = [];

        public List<Location> Locations { get; } = [];
        public List<Product> Products { get; } = [];
        public List<Order> Orders { get; } = [];

        public void Build()
        {
            BuildLocations();
            BuildScenarioProductsAndStock();
            BuildBulkProductsAndStock();
            BuildScenarioOrders();
            BuildBulkOrders();
            ApplyReservedTotals();
        }

        private void BuildLocations()
        {
            for (var aisle = 1; aisle <= 10; aisle++)
            for (var level = 1; level <= 3; level++)
            {
                AddLocation($"A-{aisle:00}-{level:00}", Zone.Pick);
            }

            for (var aisle = 1; aisle <= 5; aisle++)
            for (var level = 1; level <= 3; level++)
            {
                AddLocation($"B-{aisle:00}-{level:00}", Zone.Bulk);
            }

            for (var i = 1; i <= 3; i++) AddLocation($"RECV-{i:00}", Zone.Recv);
            for (var i = 1; i <= 4; i++) AddLocation($"QC-{i:00}", Zone.Qc);
            for (var i = 1; i <= 3; i++) AddLocation($"DMG-{i:00}", Zone.Dmg);
        }

        private void BuildScenarioProductsAndStock()
        {
            AddProduct(ChainSku, "KMC X11 chain 11-speed", reorderLevel: 2);
            AddStock(ChainSku, "A-01-01", 6);
            AddStock(ChainSku, "B-01-01", 20);

            AddProduct(TireSku, "Continental Grand Prix 5000 tire 700x28C", reorderLevel: 5);
            AddStock(TireSku, "A-01-02", 8);
            AddStock(TireSku, "B-01-03", 12);

            AddProduct(BrakePadSku, "Shimano B01S resin disc brake pads", reorderLevel: 8);
            AddStock(BrakePadSku, "A-01-03", 10);
            AddStock(BrakePadSku, "B-01-02", 4);
            AddStock(BrakePadSku, "QC-01", 4);

            AddProduct(SaddleSku, "Selle Italia Flite Boost saddle", reorderLevel: 10);
            AddStock(SaddleSku, "A-02-01", 12);

            AddProduct(GripSku, "Ergon GC1 grips", reorderLevel: 15);
            AddStock(GripSku, "A-02-02", 8);
            AddStock(GripSku, "QC-02", 10);

            AddProduct(PedalSku, "Shimano PD-M520 SPD pedals", reorderLevel: 6);
            AddStock(PedalSku, "A-02-03", 6);

            AddProduct(TubeSku, "Schwalbe SV17 inner tube 28 inch", reorderLevel: 20);
            AddStock(TubeSku, "A-03-01", 3);

            AddProduct(CassetteSku, "Shimano CS-HG50 cassette 11-32", reorderLevel: 5);
            AddStock(CassetteSku, "A-03-02", 5);
            AddStock(CassetteSku, MismatchBin, 20);
            AddStock(CassetteSku, "RECV-01", 10);
            AddStock(CassetteSku, "DMG-01", 3);

            AddProduct(BottleCageSku, "Elite Custom Race bottle cage", reorderLevel: 10);
            AddStock(BottleCageSku, "A-04-01", 100);
        }

        private void BuildBulkProductsAndStock()
        {
            foreach (var (baseSku, name, variants) in BulkCatalog)
            {
                if (variants.Length == 0)
                {
                    AddBulkProduct(baseSku, name);
                    continue;
                }

                foreach (var variant in variants)
                {
                    var suffix = variant.Replace(".", "").ToUpperInvariant();
                    AddBulkProduct($"{baseSku}-{suffix}", $"{name} {variant}");
                }
            }

            foreach (var sku in _bulkSkus)
            {
                var bins = Locations.ToArray();
                _rng.Shuffle(bins);
                foreach (var location in bins.Take(_rng.Next(1, 5)))
                {
                    // Some empty stock rows, as real systems keep them around.
                    var quantity = _rng.NextDouble() < 0.1 ? 0 : _rng.Next(1, 61);
                    AddStock(sku, location.BinCode, quantity);
                }
            }
        }

        private void AddBulkProduct(string sku, string name)
        {
            AddProduct(sku, name, reorderLevel: _rng.Next(5, 31));
            _bulkSkus.Add(sku);
        }

        private void BuildScenarioOrders()
        {
            var shortOrder = AddOrder(ShortOrder, "Fietsenwinkel De Spaak", "PSH", today);
            Reserve(AddLine(shortOrder, ChainSku, 4), "A-01-01", 4);
            Reserve(AddLine(shortOrder, TireSku, 10, shipped: 6), "A-01-02", 4);
            AddLine(shortOrder, BrakePadSku, 12);

            var brakeOther = AddOrder(BrakePadReservationOrder, "Velo Amsterdam", "ALC", today.AddDays(10));
            Reserve(AddLine(brakeOther, BrakePadSku, 4), "A-01-03", 4);

            var saddle = AddOrder(SaddleReservationOrder, "Wielerhuis Zwolle", "ALC", today.AddDays(7));
            Reserve(AddLine(saddle, SaddleSku, 5), "A-02-01", 5);

            var cassette = AddOrder(CassetteReservationOrder, "Bike Totaal Utrecht", "ALC", today.AddDays(12));
            Reserve(AddLine(cassette, CassetteSku, 2), MismatchBin, 2);

            AddLine(AddOrder(LateNew, "Rijwielzaak Bakker", "NW", today.AddDays(-5)), BottleCageSku, 2);
            AddLine(AddOrder(ShippedInPast, "Fietsdokter Haarlem", "SHP", today.AddDays(-3)), BottleCageSku, 2, shipped: 2);
            AddLine(AddOrder(OnHoldDueTomorrow, "Pedaal & Co", "HLD", today.AddDays(1)), BottleCageSku, 1);
            AddLine(AddOrder(LateLowercaseStatus, "Cycle Center Groningen", "pck", today.AddDays(-2)), BottleCageSku, 3);
            AddLine(AddOrder(ShortDueInTwoDays, "Fietsplus Eindhoven", "NW", today.AddDays(2)), TubeSku, 10);
            AddLine(AddOrder(ShortDueInThreeDays, "De Fietsfabriek Leiden", "NW", today.AddDays(3)), TubeSku, 10);
            AddLine(AddOrder(UnknownStatusDueTomorrow, "Tweewieler Centrum Gouda", "X9", today.AddDays(1)), BottleCageSku, 1);
            AddLine(AddOrder(CancelledInPast, "Rijwielhandel Van Dijk", "CNX", today.AddDays(-4)), BottleCageSku, 1);
            AddLine(AddOrder(BackorderDueInTwoDays, "Velo Amsterdam", "BO", today.AddDays(2)), BottleCageSku, 2);
            AddLine(AddOrder(HealthyDueTomorrow, "Bike Totaal Utrecht", "NW", today.AddDays(1)), BottleCageSku, 1);
        }

        private void BuildBulkOrders()
        {
            for (var number = 1000; number < 1100; number++)
            {
                if (number == ShortOrder)
                {
                    continue;
                }

                var customer = Customers[_rng.Next(Customers.Length)];
                var skus = _bulkSkus.ToArray();
                _rng.Shuffle(skus);
                var lineSkus = skus.Take(_rng.Next(1, 5)).ToList();

                if (_rng.NextDouble() < 0.45)
                {
                    var status = _rng.NextDouble() < 0.85 ? "SHP" : "CNX";
                    var order = AddOrder(number, customer, status, today.AddDays(-_rng.Next(3, 61)));
                    foreach (var sku in lineSkus)
                    {
                        var quantity = _rng.Next(1, 11);
                        AddLine(order, sku, quantity, shipped: status == "SHP" ? quantity : 0);
                    }
                }
                else
                {
                    // Open bulk orders stay outside the at-risk window so scenarios remain exact.
                    var status = OpenStatuses[_rng.Next(OpenStatuses.Length)];
                    var resolved = OrderStatus.Resolve(status).Code;
                    var order = AddOrder(number, customer, status, today.AddDays(_rng.Next(5, 31)));
                    foreach (var sku in lineSkus)
                    {
                        var quantity = _rng.Next(1, 11);
                        var line = AddLine(order, sku, quantity, shipped: resolved == "PSH" ? _rng.Next(0, quantity) : 0);
                        if (resolved is "ALC" or "PCK" or "PSH")
                        {
                            ReserveFromFreeStock(line);
                        }
                    }
                }
            }
        }

        private void ApplyReservedTotals()
        {
            foreach (var stock in Products.SelectMany(p => p.StockLevels))
            {
                stock.Reserved = _reserved.GetValueOrDefault(stock);
            }

            // Deliberate drift between the stored total and the reservation rows.
            FindStock(CassetteSku, MismatchBin).Reserved += 1;
        }

        private void AddLocation(string binCode, Zone zone)
        {
            var location = new Location { BinCode = binCode, Zone = zone };
            _locations.Add(binCode, location);
            Locations.Add(location);
        }

        private void AddProduct(string sku, string name, int reorderLevel)
        {
            var product = new Product { Sku = sku, Name = name, ReorderLevel = reorderLevel };
            _products.Add(sku, product);
            Products.Add(product);
        }

        private void AddStock(string sku, string binCode, int quantity)
        {
            var product = _products[sku];
            product.StockLevels.Add(new StockLevel { Product = product, Location = _locations[binCode], Quantity = quantity });
        }

        private Order AddOrder(int number, string customer, string statusCode, DateOnly promisedDate)
        {
            var order = new Order { OrderNumber = number, Customer = customer, StatusCode = statusCode, PromisedDate = promisedDate };
            Orders.Add(order);
            return order;
        }

        private OrderLine AddLine(Order order, string sku, int quantity, int shipped = 0)
        {
            var line = new OrderLine { Order = order, Product = _products[sku], Quantity = quantity, QuantityShipped = shipped };
            order.Lines.Add(line);
            return line;
        }

        private void Reserve(OrderLine line, string binCode, int quantity)
        {
            var stock = FindStock(line.Product.Sku, binCode);
            line.Reservations.Add(new Reservation { OrderLine = line, Location = stock.Location, Quantity = quantity });
            _reserved[stock] = _reserved.GetValueOrDefault(stock) + quantity;
        }

        private void ReserveFromFreeStock(OrderLine line)
        {
            var remaining = line.QuantityOpen;
            var shippable = line.Product.StockLevels
                .Where(s => s.Location.Zone.IsShippable())
                .OrderBy(s => s.Location.BinCode, StringComparer.Ordinal);

            foreach (var stock in shippable)
            {
                var take = Math.Min(remaining, stock.Quantity - _reserved.GetValueOrDefault(stock));
                if (take > 0)
                {
                    Reserve(line, stock.Location.BinCode, take);
                    remaining -= take;
                }
            }
        }

        private StockLevel FindStock(string sku, string binCode) =>
            _products[sku].StockLevels.Single(s => s.Location.BinCode == binCode);
    }
}
