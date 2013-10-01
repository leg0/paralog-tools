#!/usr/bin/python

import cgi
#import cgitb; cgitb.enable()
import libxml2
import libxslt
import re

DATABASE_FILE = "db.xml"

print "Content-Type: application/json\r\n"

class MonthStats:
    def __init__(self, num):
        self.num = num
        self.count = 0
        self.min = 1000000
        self.max = 0

    def add(self, jumpNumber):
        self.count += 1
        if jumpNumber < self.min:
            self.min = jumpNumber

        if jumpNumber > self.max:
            self.max = jumpNumber

    def toString(self):
        return '{"num":"%s","count":%d,"min":%s,"max":%s}' % ( self.num, self.count, self.min, self.max )

    # end MonthStats

class YearStats:
    def __init__(self, num):
        self.num = num
        self.count = 0
        self.months = {}

    def increment(self):
        self.count += 1

    def addMonth(self, month, jumpNumber):
        self.increment()

        if not month in self.months:
            self.months[month] = MonthStats(month)

        self.months[month].add(jumpNumber)
    
    # Converts the stats to a JSON string.
    #
    def toString(self):
        arrMonths = [ self.months[y].toString() for y in self.months ]
        arrMonths.sort()
        strMonths = ','.join(arrMonths)
        return '{"num":%s, "count":%d, "months":[%s]}' % (self.num, self.count, strMonths)

    # end YearStats


# Counts the jumps by months and accumulates by year.
def groupJumpsByYear(paralogXmlFile):
    reader = libxml2.newTextReaderFilename(paralogXmlFile)
    reader.Read()
    node = reader.Expand()
    years = {}
    for n in node.xpathEval("/pml/log/jump"):
        ts = n.xpathEval("@ts")[0].content
        n = int(n.xpathEval("@n")[0].content)
        m = re.match('^([0-9]{4})-([0-9]{2}).*', ts)
        if m:
            year = m.group(1)
            month = m.group(2)
            if not year in years:
                years[year] = YearStats(year)
            years[year].addMonth(month, n)
    return years

jumps = groupJumpsByYear(DATABASE_FILE)

arr = [ jumps[y].toString() for y in jumps ]
arr.sort()

print '{"years":['
print ",\n".join(reversed(arr))
print ']}'

