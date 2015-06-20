package main

import (
	"code.google.com/p/go-sqlite/go1/sqlite3"
	"compress/gzip"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strconv"
	"strings"
)

const DATABASE_FILE = "sqlite.db"
var sqliteConn *sqlite3.Conn

type gzipResponseWriter struct {
	io.Writer
	http.ResponseWriter
}

func (w gzipResponseWriter) Write(b []byte) (int, error) {
	return w.Writer.Write(b)
}

func makeGzipHandler(fn http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if !strings.Contains(r.Header.Get("Accept-Encoding"), "gzip") {
			fn(w, r)
		} else {
			w.Header().Set("Content-Encoding", "gzip")
			gz := gzip.NewWriter(w)
			defer gz.Close()
			gzr := gzipResponseWriter{Writer: gz, ResponseWriter: w}
			fn(gzr, r)
		}
	}
}

// Counts the jumps by months and accumulates by year.
//
func groupJumpsByYear()([]byte, error) {
	type MonthGroup struct {
		Num   int `json:"num"`
		Count int `json:"count"`
		Min   int `json:"min"`
		Max   int `json:"max"`
	}

	type YearMonthGroup struct {
		Num    int          `json:"num"`
		Count  int          `json:"count"`
		Months []MonthGroup `json:"months"`
	}

	type Years struct {
		Years []YearMonthGroup `json:"years"`
	}

	yrs := make([]YearMonthGroup, 0)

	sql := `
		select Y, M, count(*) C, min(n), max(n) 
		from (
			select
				cast(strftime('%Y', ts) as integer) Y,
				cast(strftime('%m', ts) as integer) M,
				n
			from jump)
		group by Y, M
		order by Y desc`
	prevYear := 0
	ymg := YearMonthGroup{}
	for s, err := sqliteConn.Query(sql); err == nil; err = s.Next() {
		y, m, c, mn, mx := 0, 0, 0, 0, 0
		s.Scan(&y, &m, &c, &mn, &mx)

		if prevYear == y {
			ymg.Count += c
			ymg.Months = append(ymg.Months, MonthGroup{m, c, mn, mx})
		} else {
			if ymg.Num != 0 {
				yrs = append(yrs, ymg)
			}
			prevYear = y

			ymg = YearMonthGroup{y, c, []MonthGroup{MonthGroup{m, c, mn, mx}}}
		}
	}
	if ymg.Num != 0 {
		yrs = append(yrs, ymg)
	}
	return json.Marshal(Years{yrs})
}

func groupJumpsByDropzone()([]byte, error) {
	type DzGroup struct {
		Name string `json:"dz"`
		Count int `json:"count"`
	}

	type AllDz struct {
		X []DzGroup `json:"by_dz"`
	}

	sql := `
		select d.name dz, count(*) c
		from jump j
			left outer join dropzone d on j.dz_id=d.rowid
		group by j.dz_id
		order by dz`

	g := make([]DzGroup, 0)
	for s, err := sqliteConn.Query(sql); err == nil; err = s.Next() {
		dg := DzGroup {}
		s.Scan(&dg.Name, &dg.Count)
		g = append(g, dg)
	}
	return json.Marshal(AllDz { g })
}

func groupJumpsByAircraft()([]byte, error) {
	type AcGroup struct {
		Name string `json:"ac"`
		Count int `json:"count"`
	}

	type AllAc struct {
		X []AcGroup `json:"by_ac"`
	}

	sql := `
		select a.type ac, count(*) c
		from jump j
			left outer join aircraft a on j.ac_id=a.rowid
		group by j.ac_id
		order by ac`
	
	g := make([]AcGroup, 0)
	for s, err := sqliteConn.Query(sql); err == nil; err = s.Next() {
		dg := AcGroup {}
		s.Scan(&dg.Name, &dg.Count)
		g = append(g, dg)
	}
	return json.Marshal(AllAc { g })
}

// Execute a query that returns a single row with one value.
func queryValue(c *sqlite3.Conn, sql string) (int, error) {
	if s, err := c.Query(sql); err == nil {
		var res int
		s.Scan(&res)
		return res, nil
	} else {
		return 0, err
	}
}

// Get statistics: total jumps, jumps in last 3 months, number of dz jumped at
// number of aircraft jumped from.
func getBasicStats()([]byte, error) {
	type StatsDetails struct {
		Total       int `json:"jumps_total"`
		LastYear    int `json:"jumps_last_year"`
		Last3Months int `json:"jumps_last_3months"`
		Dropzones   int `json:"dropzones"`
		Aircraft    int `json:"aircraft"`
	}
	type Stats struct {
		S StatsDetails `json:"stats"`
	}
	stats := StatsDetails{}
	var err error
	c := sqliteConn
	if stats.Total, err = queryValue(c, "select count(*) from jump"); err != nil {
		return nil, err
	}

	if stats.LastYear, err = queryValue(c, "select count(*) from jump where ts >= datetime('now', '-1 year')"); err != nil {
		return nil, err
	}

	if stats.Last3Months, err = queryValue(c, "select count(*) from jump where ts >= datetime('now', '-3 month')"); err != nil {
		return nil, err
	}

	if stats.Dropzones, err = queryValue(c, "select count(*) from dropzone"); err != nil {
		return nil, err
	}

	if stats.Aircraft, err = queryValue(c, "select count(*) from aircraft"); err != nil {
		return nil, err
	}

	return json.Marshal(Stats{stats})
}

func getJumpDetails(jumpNumber int)([]byte, error) {
	type JumpDetails struct {
		Num int `json:"num"`
		Time string `json:"time"`
		Aircraft string `json:"aircraft"`
		Dropzone string `json:"dropzone"`
		Exit int `json:"exit"`
		Open int `json:"open"`
		//Delay int `json:"delay"`
		Type string `json:"type"`
	}

	sql := "select j.n, ac.type, dz.name, j.exit, j.open, j.type, j.ts " +
		"from (select * from jump where n=@n) j" +
			" left outer join aircraft ac on j.ac_id = ac.rowid" +
			" left outer join dropzone dz on j.dz_id = dz.rowid"

	if stmt, err := sqliteConn.Query(sql, jumpNumber); err == nil {
		j := JumpDetails {}
		stmt.Scan(&j.Num, &j.Aircraft, &j.Dropzone, &j.Exit, &j.Open, &j.Type, &j.Time)
		return json.Marshal(j)
	} else {
		return nil, err
	}
}

func handler(w http.ResponseWriter, r *http.Request, f func()([]byte, error)) {
	if json, err := f(); err == nil {
		header := w.Header()
		header.Set("Content-Type", "application/json; charset=utf-8")
		w.Write(json)
	} else {
		w.WriteHeader(http.StatusInternalServerError)
		fmt.Fprintln(w, err) // TODO: only if debugging enabled
	}
}

func statsHandler(w http.ResponseWriter, r *http.Request) {
	fmt.Println("/stats")
	handler(w, r, getBasicStats)
}

func yearsHandler(w http.ResponseWriter, r *http.Request) {
	fmt.Println("/years")
	handler(w, r, groupJumpsByYear)
}

func jumpHandler(w http.ResponseWriter, r *http.Request) {
	if i, err := strconv.Atoi(r.FormValue("n")); err == nil {
		handler(w, r, func()([]byte, error) { return getJumpDetails(i) })
	} else {
		http.NotFound(w, r)
	}
}

func makeHandler(f func()([]byte, error)) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
			handler(w, r, f)
		}
}

func main() {
	fmt.Println("logbook server starting")
	if c, e := sqlite3.Open(DATABASE_FILE); e == nil {
		fmt.Println("logbook database opened")
		sqliteConn = c

		mux := http.NewServeMux()
		mux.HandleFunc("/x/stats", statsHandler)
		mux.HandleFunc("/x/group-by-year", makeGzipHandler(yearsHandler))
		mux.HandleFunc("/x/group-by-dropzone", makeGzipHandler(makeHandler(groupJumpsByDropzone)))
		mux.HandleFunc("/x/group-by-aircraft", makeGzipHandler(makeHandler(groupJumpsByAircraft)))
		mux.HandleFunc("/x/jump", makeGzipHandler(jumpHandler))
		http.ListenAndServe("localhost:8080", mux)
	} else {
		fmt.Println(e)
	}
}
