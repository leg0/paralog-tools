package main

import (
	"code.google.com/p/go-sqlite/go1/sqlite3"
	"encoding/json"
	"fmt"
	"net/http"
)

const DATABASE_FILE = "sqlite.db"
var sqliteConn *sqlite3.Conn	

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

	sql := "select Y, M, count(*) C, min(n), max(n) from (select cast(strftime('%Y', ts) as integer) Y, cast(strftime('%m', ts) as integer) M, n from jump) group by Y, M"
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

func main() {
	fmt.Println("logbook server starting")
	if c, e := sqlite3.Open(DATABASE_FILE); e == nil {
		fmt.Println("logbook database opened")
		sqliteConn = c
		
		mux := http.NewServeMux()
		mux.HandleFunc("/x/stats", statsHandler)
		mux.HandleFunc("/x/years", yearsHandler)
		mux.Handle("/", http.FileServer(http.Dir("./")))
		http.ListenAndServe("localhost:8080", mux)
	} else {
		fmt.Println(e)
	}
}
