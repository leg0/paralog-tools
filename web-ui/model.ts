/// <reference path="DefinitelyTyped/jquery/jquery.d.ts" />
/// <reference path="DefinitelyTyped/knockout/knockout.d.ts" />
/// <reference path="DefinitelyTyped/sammyjs/sammyjs.d.ts" />

var model : AppViewModel;

$(function ()
{
    model = new AppViewModel();
    ko.applyBindings(model);
});

class YearModel {

    num: KnockoutObservable<number>;
    count: KnockoutObservable<number>;
    months: KnockoutObservableArray<MonthModel>;

    constructor(y)
    {
        this.num = ko.observable(y.num)
        this.count = ko.observable(y.count)
        this.months = ko.observableArray(y.months.map((m) => { return new MonthModel(this, m) }))
    }

    // Navigates to an address of the year and optionally month of the year.
    // @param month - MonthModel
    navigateTo(month: MonthModel, jump?: number)
    {

        var hash = "#/jumps/date/" + this.num()
        if (month)
        {
            hash = hash + "/" + month.num
        }

        if (jump)
        {
            hash = hash + "/jump/" + jump
        }

        location.hash = hash
    }

    findMonthByNum(monthNum: number): MonthModel
    {
        var res = null
        this.months().forEach((x) =>
        {
            if (x.num == monthNum)
                res = x
        })
        return res
    }

}

class MonthModel
{
    year: number;
    num: number;
    name: string;
    count: number;
    min: number;
    max: number;

    constructor(y: YearModel, m)
    {
        this.year = y.num()
        this.num = parseInt(m.num)
        this.name = MonthModel.monthNumberToName(this.num)
        this.count = m.count
        this.min = m.min
        this.max = m.max
    }

    static monthNumberToName(n: number): string
    {
        var names = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
        return names[n - 1]
    }
}

class AircraftModel
{
    constructor(public name: string, public link: string)
    {
    }
}

class DropzoneModel
{
    constructor(public name: string, public lat: number, public lon: number)
    {
    }
}

class JumpModel
{
    isLoaded: KnockoutObservable<boolean> = ko.observable(false);
    num: KnockoutObservable<number> = ko.observable(1234);
    aircraft: KnockoutObservable<AircraftModel> = ko.observable();
    dropzone: KnockoutObservable<string> = ko.observable();
    exit: KnockoutObservable<number> = ko.observable();
    open: KnockoutObservable<number> = ko.observable();
    delay: KnockoutObservable<number> = ko.observable();
    type: KnockoutObservable<string> = ko.observable("Wingsuit");
    equipment: KnockoutObservable<string> = ko.observable();
    //profile: KnockoutComputed<number>; // points

    jump: any = null;

    // TODO: asynchronous loading of data
    constructor(num: number)
    {
        this.num(num);
        this.asyncLoad();
    }

    asyncLoad()
    {
        // TODO: get jump details from server. on success set isLoaded to true.
    }
}

// View models

interface ViewModel
{
}

class JumpsTabViewModel implements ViewModel
{

    years: KnockoutObservableArray<YearModel>;
    count: KnockoutComputed<number>;
    selectedYear: KnockoutObservable<YearModel>;
    selectedMonth: KnockoutObservable<MonthModel>;
    selectedJump: KnockoutObservable<JumpModel>;

    constructor()
    {
        var years = this.years = ko.observableArray()
        this.count = ko.computed(() =>
        {
            var ys = years()
            return ys
                ? ys.reduce((acc, y) => { return acc + y.count(); }, 0)
                : 0;
        })
        this.selectedYear = ko.observable()
        this.selectedMonth = ko.observable()
        this.selectedJump = ko.observable()

        this.getYearsAsync()
    }

    // operations

    getYearsAsync(fn?: Function)
    {
        $.getJSON("./years.cgi", (data) =>
        {
            if (data && data.years)
                this.years(data.years.map((y) => { return new YearModel(y) }))
                if (fn) fn()
        })
        //.done(function() { console.log( "second success" ) })
        //.fail(function(e) { console.log( "error", e ) })
        //.always(function() { console.log( "complete" ) })
    }

    selectYearX(year: number, month?: number)
    {
        var ys = this.years()
        if (ys.length == 0)
        {
            this.getYearsAsync(() => { this.selectYearX(year, month) })
            return
        }

        // Try to find a YearModel object for given year number.
        for (var x = 0; x < ys.length; ++x)
        {
            if (ys[x].num() == year)
            {

                // found a year, deal with it
                var y = ys[x]
                this.selectYear(y, y.findMonthByNum(month))
                return
            }
        }

        console.log("Invalid year", year)
    }

    // @param year - an instance of YearModel / number
    // @param month - an instance on MonthModel / number
    selectYear(year: YearModel, month: MonthModel)
    {

        var yearNum = year.num();
        //console.log("Selecting year: " , year, yearNum);
        var currentYear = this.selectedYear();
        this.selectedYear(year);
        if (currentYear && currentYear.num() != yearNum)
        {
            this.selectedJump(null)
            this.selectedMonth(null)
        }

        this.selectedMonth(month)
    }

    navigateDate(year?: number, month?: number, day?: number)
    {
        var hash = "#/jumps"
        if (year)
        {
            hash = hash + "/date/" + year
            if (month)
            {
                hash = hash + "/" + month
                if (day)
                {
                    hash = hash + "/" + day
                }
            }
        }

        location.hash = hash
    }
}

class StatisticsTabViewModel implements ViewModel
{
}

class UploadTabViewModel implements ViewModel
{
}

class AppViewModel implements ViewModel
{

    title: string = "YOUR NAME's skydiving logbook";
    activeTab: KnockoutObservable<string> = ko.observable('jumps');

    tabs: ViewModel[] = [
        new JumpsTabViewModel(),
        new StatisticsTabViewModel(),
        new UploadTabViewModel()
    ];

    jumpsTab: KnockoutObservable<any> = ko.observable(this.tabs[0]);
    statsTab: KnockoutObservable<any> = ko.observable();
    uploadTab: KnockoutObservable<any> = ko.observable();
    logTab: KnockoutObservable<any> = ko.observable();
    contactsTab: KnockoutObservable<any> = ko.observable();

    constructor()
    {
        initSammy(this);
    }

    // operations

    deselectAllTabs_()
    {
        this.jumpsTab(null);
        this.statsTab(null);
        this.uploadTab(null);
        this.logTab(null);
        this.contactsTab(null);
    }

    selectTab(tabId: string)
    {
        this.deselectAllTabs_();
        switch (tabId)
        {
            case 'jumps':  this.jumpsTab(this.tabs[0]); break;
            case 'stats':  this.statsTab(this.tabs[1]); break;
            case 'upload': this.uploadTab(this.tabs[2]); break;
            case 'log':    this.contactsTab(this.tabs[3]); break;
            case 'conta':  this.contactsTab(this.tabs[4]); break;
        }

        this.activeTab(tabId);
    }
}

function initSammy(self: AppViewModel)
{
    var jumpArray: JumpModel[] = [];
    function findJump(jumpNum: number): JumpModel
    {
        if (!jumpArray[jumpNum])
            jumpArray[jumpNum] = new JumpModel(jumpNum);

        return jumpArray[jumpNum];
    }

    Sammy(function ()
    {
        this.get("#/jumps/date/:yearNum/:monthNum/jump/:jumpNum", function ()
        {
            self.selectTab('jumps')
            var jt = self.jumpsTab()
            jt.selectYearX(parseInt(this.params.yearNum), parseInt(this.params.monthNum))
            jt.selectedJump(findJump(this.params.jumpNum))
        })
        this.get("#/jumps/date/:yearNum/:monthNum", function ()
        {
            self.selectTab('jumps')
            var jt = self.jumpsTab()
            jt.selectYearX(parseInt(this.params.yearNum), parseInt(this.params.monthNum))
        })
        this.get("#/jumps/date/:yearNum", function ()
        {
            self.selectTab('jumps')
            var jt = self.jumpsTab()
            jt.selectYearX(parseInt(this.params.yearNum))
        })
        this.get("#/jumps", function ()
        {
            self.selectTab('jumps')
            self.jumpsTab().selectedYear(undefined)
            self.jumpsTab().selectedMonth(undefined)
            self.jumpsTab().selectedJump(undefined)
        })
        this.get("#/stats", function () { self.selectTab('stats') })
        this.get("#/upload", function () { self.selectTab('upload') })
        this.get("#/log", function () { self.selectTab('log') })
        this.get("#/contact", function () { self.selectTab('contact') })
        //this.get(".*", function() { location.hash = "#/jumps" })
    }).run();
}