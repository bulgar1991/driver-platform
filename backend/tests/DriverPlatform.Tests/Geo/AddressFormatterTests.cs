using DriverPlatform.Infrastructure.Maps;

namespace DriverPlatform.Tests.Geo;

public class AddressFormatterTests
{
    [Fact]
    public void A_point_on_the_map_drops_the_shop_that_happens_to_be_there()
    {
        var parts = new AddressParts(
            Name: "Re Bolla - Tabacchi Giochi & Scommesse", Road: "Via Luisa Battistotti Sassi", HouseNumber: "24",
            Postcode: "20133", City: "Milan", Suburb: "Corsica", Region: "Lombardy");

        var result = AddressFormatter.Format(parts, withName: false);

        Assert.Equal(("Via Luisa Battistotti Sassi 24, Milan", "Via Luisa Battistotti Sassi 24, 20133 Milan"), result);
    }

    [Fact]
    public void A_searched_place_keeps_its_name_for_the_driver()
    {
        var parts = new AddressParts(
            Name: "Hotel Danieli", Road: "Riva degli Schiavoni", HouseNumber: "4196", Postcode: "30122", City: "Venice");

        var result = AddressFormatter.Format(parts, withName: true);

        Assert.Equal(("Hotel Danieli, Venice", "Hotel Danieli, Riva degli Schiavoni 4196, 30122 Venice"), result);
    }

    [Fact]
    public void A_whole_city_reads_city_and_region()
    {
        var result = AddressFormatter.Format(new AddressParts(Name: "Rome", Postcode: "00118", City: "Rome", Region: "Lazio"), withName: true);

        Assert.Equal(("Rome, Lazio", "Rome, Lazio"), result);
    }

    [Fact]
    public void Without_a_street_the_district_says_where()
    {
        var result = AddressFormatter.Format(
            new AddressParts(Postcode: "00068", City: "Rignano Flaminio", Suburb: "Sambuco", Region: "Lazio"), withName: false);

        Assert.Equal(("Sambuco, Rignano Flaminio", "Sambuco, 00068 Rignano Flaminio"), result);
    }

    [Fact]
    public void Venice_numbers_go_with_the_district()
    {
        var result = AddressFormatter.Format(
            new AddressParts(HouseNumber: "1", Postcode: "30124", City: "Venice", Suburb: "San Marco"), withName: false);

        Assert.Equal(("San Marco 1, Venice", "San Marco 1, 30124 Venice"), result);
    }

    [Fact]
    public void An_empty_name_is_no_name()
    {
        var result = AddressFormatter.Format(
            new AddressParts(Name: "", Road: "Via del Corso", HouseNumber: "12", Postcode: "00187", City: "Rome"), withName: true);

        Assert.Equal(("Via del Corso 12, Rome", "Via del Corso 12, 00187 Rome"), result);
    }

    [Fact]
    public void A_road_that_is_only_a_number_is_left_out()
    {
        var result = AddressFormatter.Format(
            new AddressParts(Name: "Stazione Termini", Road: "1", Postcode: "00185", City: "Rome"), withName: true);

        Assert.Equal(("Stazione Termini, Rome", "Stazione Termini, 00185 Rome"), result);
    }

    [Fact]
    public void Nothing_usable_gives_nothing()
    {
        Assert.Null(AddressFormatter.Format(new AddressParts(), withName: true));
    }
}
