/// <reference path="Scripts/typings/jquery/jquery.d.ts" />
/// <reference path="Scripts/typings/knockout/knockout.d.ts" />
/// <reference path="Scripts/typings/sammyjs/sammyjs.d.ts" />

var model : AppViewModel;

$(function ()
{
    model = new AppViewModel();
    ko.applyBindings(model);
});

class YearModel {

    num: KnockoutObservable<number> = ko.observable(0);
    count: KnockoutObservable<number> = ko.observable(0);
    months: KnockoutObservableArray<MonthModel> = ko.observableArray([]);

    constructor(y)
    {
        this.num(y.num);
        this.count(y.count);
        if (y.months) {
            this.months(y.months.map((m) => { return new MonthModel(this, m) }));
        }
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
        this.min = m.min;
        this.max = m.max;
    }

    range(): number[] { return range(this.min, this.max); }

    private static monthNumberToName(n: number): string
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
    num: KnockoutObservable<number> = ko.observable(0);
    time: KnockoutObservable<string> = ko.observable("");
    aircraft: KnockoutObservable<AircraftModel> = ko.observable(null);
    dropzone: KnockoutObservable<string> = ko.observable(null);
    exit: KnockoutObservable<number> = ko.observable(null);
    open: KnockoutObservable<number> = ko.observable(null);
    delay: KnockoutObservable<number> = ko.observable(null);
    type: KnockoutObservable<string> = ko.observable("");
    equipment: KnockoutObservable<string> = ko.observable("");
    //profile: KnockoutComputed<number>; // points

    jump: any = null;

    constructor(num: number)
    {
        this.num(num)
        this.asyncLoad(num)
    }

    asyncLoad(n: number)
    {
        // get jump details from server. on success set isLoaded to true.
        $.getJSON("./x/jump?n=" + n, (j) => {
            console.log("Got jump", n, "details:", j)
            if (j)
            {
                this.time(j.time)
                this.aircraft(j.aircraft)
                this.dropzone(j.dropzone)
                this.exit(j.exit)
                this.open(j.open)
                this.delay(j.delay)
                this.type(j.type)
                this.isLoaded(true)
                // TODO: profile and other interesting stuff.
            }
        })
    }
}

// View models

interface ViewModel
{
}

class JumpsTabViewModel implements ViewModel
{
    years: KnockoutObservableArray<YearModel> = ko.observableArray([])
    count: KnockoutComputed<number>;
    selectedYear: KnockoutObservable<YearModel> = ko.observable(null)
    selectedMonth: KnockoutObservable<MonthModel> = ko.observable(null)
    selectedJump: KnockoutObservable<JumpModel> = ko.observable(null)

    constructor()
    {
        this.count = ko.computed(() =>
        {
            var ys = this.years()
            return ys
                ? ys.reduce((acc:number, y:YearModel) => { return acc + y.count() }, 0)
                : 0
        })

        this.getYearsAsync()
    }

    // operations

    getYearsAsync(fn?: Function)
    {
        $.getJSON("./x/group-by-year", (data) =>
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

class BasicStatsModel {
    jumpsTotal: number
    jumpsLastYear: number
    jumpsLast3months: number
    dropzones: number
    aircraft: number

    constructor(o) {
        this.jumpsTotal = o.jumps_total
        this.jumpsLastYear = o.jumps_last_year
        this.jumpsLast3months = o.jumps_last_3months
        this.dropzones = o.dropzones
        this.aircraft = o.aircraft
    }
}

class TypeGroup
{
	type: string;
	count: number;
}

class DelayGroup
{
    delay: number;
    count: number;
}

class StatisticsTabViewModel implements ViewModel
{
    basicStats: KnockoutObservable<BasicStatsModel> = ko.observable(null)
    groupedByDz: KnockoutObservableArray<any> = ko.observableArray(null)
    groupedByAc: KnockoutObservableArray<any> = ko.observableArray(null)
    groupedByType: KnockoutObservableArray<TypeGroup> = ko.observableArray([])
    groupedByDelay: KnockoutObservableArray<DelayGroup> = ko.observableArray([])

    constructor()
    {
        this.getBasicStatsAsync()
    }

    private getBasicStatsAsync() {
        $.getJSON("./x/stats", (data) => {
            if (data && data.stats) {
                this.basicStats(new BasicStatsModel(data.stats))
            }
        })
    }

    private getGroupedByDzAsync(fn? : Function) {
        $.getJSON("./x/group-by-dropzone", (data) => {
            if (data && data.by_dz) {
                this.groupedByDz(data.by_dz)
                if (fn) { fn() }
            }
        })
    }

    private getGroupedByAcAsync(fn? : Function) {
        $.getJSON("./x/group-by-aircraft", (data) => {
            if (data && data.by_ac) {
                this.groupedByAc(data.by_ac)
                if (fn) { fn() }
            }
        })
    }

    private getGroupedByTypeAsync(fn?: Function) {
		$.getJSON("./x/group-by-type", (data) => {
			if (data && data.by_type) {
				this.groupedByType(data.by_type)
				if (fn) { fn() }
			}
		})
    }

    private getGroupedByDelayAsync(fn?: Function) {
        $.getJSON("./x/group-by-delay", (data) => {
            if (data && data.Delays) {
                this.groupedByDelay(data.Delays)
                if (fn) { fn() }
            }
        })
    }

    showAcGroup() {
        this.getGroupedByAcAsync(() => {
            this.groupedByDz(null);
            this.groupedByType(null);
            this.groupedByDelay(null)
        });
    }

    showDzGroup() {
        this.getGroupedByDzAsync(() => {
            this.groupedByAc(null);
            this.groupedByType(null);
            this.groupedByDelay(null)
        });
    }

    showTypeGroup() {
		this.getGroupedByTypeAsync(() => {
			this.groupedByAc(null)
			this.groupedByDz(null)
            this.groupedByDelay(null)
		});
    }

    showDelayGroup() {
        this.getGroupedByDelayAsync(() => {
            this.groupedByAc(null)
            this.groupedByDz(null)
            this.groupedByType(null)
        });
    }
}

class UploadTabViewModel implements ViewModel
{
}

class AppViewModel implements ViewModel
{
    title: string = "lego's skydiving logbook";
    activeTab: KnockoutObservable<string> = ko.observable('');

    tabs: ViewModel[] = [];

    jumpsTab: KnockoutObservable<any> = ko.observable();
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

    selectTab(tabId: string): ViewModel
    {
        this.deselectAllTabs_();
        switch (tabId)
        {
            case 'jumps':
                if (!this.tabs[0]) {
                    this.tabs[0] = new JumpsTabViewModel()
                }
                this.jumpsTab(this.tabs[0]);
                break;

            case 'stats':
                if (!this.tabs[1]) {
                    this.tabs[1] = new StatisticsTabViewModel()
                }
                this.statsTab(this.tabs[1]);
                break;;

            case 'upload':
                console.log("WTF? Upload?")
                if (!this.tabs[2]) {
                    this.tabs[2] = new UploadTabViewModel()
                }
                this.uploadTab(this.tabs[2]);
                break;

            //case 'log':    this.contactsTab(this.tabs[3]); break;
            //case 'conta':  this.contactsTab(this.tabs[4]); break;
        }

        this.activeTab(tabId);
        return this.activeTab();
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
            self.selectTab('jumps');
            var jt = self.jumpsTab();
            jt.selectYearX(parseInt(this.params.yearNum), parseInt(this.params.monthNum));
            jt.selectedJump(findJump(this.params.jumpNum));
        })
        this.get("#/jumps/date/:yearNum/:monthNum", function ()
        {
            self.selectTab('jumps')
            var jt = self.jumpsTab()
            jt.selectYearX(parseInt(this.params.yearNum), parseInt(this.params.monthNum))
        })
        this.get("#/jumps/date/:yearNum", function ()
        {
            self.selectTab('jumps');
            var jt = self.jumpsTab();
            jt.selectYearX(parseInt(this.params.yearNum));
        })
        this.get("#/jumps", function ()
        {
            self.selectTab('jumps')
            self.jumpsTab().selectedYear(undefined)
            self.jumpsTab().selectedMonth(undefined)
            self.jumpsTab().selectedJump(undefined)
        })
        this.get("#/stats/ac", function () {
            self.selectTab('stats')
            self.statsTab().showAcGroup()
        })
        this.get("#/stats/delay", function() {
            self.selectTab('stats');
            self.statsTab().showDelayGroup();
        })
        this.get("#/stats/dz", function () {
            self.selectTab('stats');
            self.statsTab().showDzGroup();
        })
        this.get("#/stats/type", function () {
            self.selectTab('stats');
            self.statsTab().showTypeGroup();
        })
        this.get("#/stats", function ()
        {
            self.selectTab('stats')
            self.statsTab().groupedByAc(null)
            self.statsTab().groupedByDz(null)
        })
        this.get("#/upload", function () { self.selectTab('upload') })
        //this.get("#/log", function () { self.selectTab('log') })
        //this.get("#/contact", function () { self.selectTab('contact') })
        //this.get(".*", function() { location.hash = "#/jumps" })
    }).run();
}

// TODO: make it a generator. and make knockout's foreach wotk with generators.
function range(lo: number, hi: number): number[]
{
    var res = []
    for (var i = lo; i <= hi; ++i)
    {
        res.push(i)
    }
    return res
}
