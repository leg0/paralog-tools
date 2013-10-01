#!/usr/bin/python

import cgi
# import cgitb; cgitb.enable()
import os, sys
import gzip
import libxml2
import libxslt


UPLOAD_DIR = "/path/to/upload/dir"

HTML_TEMPLATE = """<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html><head><title>File Upload</title>
<meta http-equiv="Refresh" content="0;url=./#/upload">
</body>
</html>"""

def print_html_form ():
    print "Content-Type: text/html\r\n"
    print HTML_TEMPLATE % {'SCRIPT_NAME':os.environ['SCRIPT_NAME']}

def save_uploaded_file (form_field, upload_dir):
    form = cgi.FieldStorage()
    if not form.has_key(form_field): return False
    fileitem = form[form_field]
    if not fileitem.file: return False
    gzf = gzip.GzipFile(fileobj = fileitem.file, mode='rb')
    fout = file (os.path.join(upload_dir, "tmp.xml"), 'wb')
    while 1:
        chunk = gzf.read(1000000)
        if not chunk: break
        fout.write (chunk)
    fout.close()
    return True



print_html_form()
if save_uploaded_file ("db", UPLOAD_DIR):
    srcFile = UPLOAD_DIR + "/tmp.xml"
    os.rename(srcFile, "db.xml")

