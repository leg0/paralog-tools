#!/home/lego/bin/python

import cgi
#import cgitb; cgitb.enable()
import libxml2
import libxslt
import re

DATABASE_FILE = "db.xml"


class JumpData:
    def __init__(self):
        self.n = 0

    def toString(self):
        return '{"num":%d,"aircraft":"dropzone":"%s","%s","exit":%d,"open":%d,"type":"%s"}' % ( self.n, self.aircraft, self.dropzone, self.exit, self.open, self.type )

# Counts the jumps by months and accumulates by year.
def getJump(paralogXmlFile, jumpNum):
    reader = libxml2.newTextReaderFilename(paralogXmlFile)
    reader.Read()
    node = reader.Expand()
    for jumpNode in node.xpathEval("/pml/log/jump"):
        n = int(jumpNode.xpathEval("@n")[0].content)
        if n ==  jumpNum:
            jump = JumpData()
            jump.n = n
            jump.dropzone = jumpNode.xpathEval("dz")[0].content
            jump.aircraft = jumpNode.xpathEval("ac")[0].content
            jump.type     = jumpNode.xpathEval("type")[0].content
            jump.exit     = int(jumpNode.xpathEval("exit")[0].content)
            jump.open     = int(jumpNode.xpathEval("open")[0].content)
            jump.delay    = int(jumpNode.xpathEval("ffTime")[0].content)
            return jump

    print "Did not find jump", jumpNum
    return None


args = cgi.FieldStorage()
if "jumpNum" in args:
    jump = getJump(DATABASE_FILE, int(args["jumpNum"].value))
    if jump != None:
        print "Content-Type: application/json\r\n"
        print '{"jump":'
        print jump.toString()
        print '}'
    else:
        # 404, not found
        print "Status: 404"
        print "Content-Type: text/plain\r\n\r\n"
        print "Not found"
else:
    print "Status: 400\r\n"
    print "Bad request"
