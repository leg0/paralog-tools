sub printTime
{
    use integer;
	my $t = shift;
	$ms = $t % 1000;
	$t /= 1000;
	$s = $t % 60;
	$t /= 60;
	$m = $t % 60;
	$h = $t / 60;

	printf "%02d:%02d:%02d,%03d", $h, $m, $s, $ms;
}

$headings = <>;
$n = 1;
$t = 0;
while (<>)
{
	@fields = split /,/;
	$datetime = $fields[0];
	$vspeed = $fields[6];
	$nspeed = $fields[4];
	$espeed = $fields[5];
	$hMsl   = $fields[3];
	$hspeed = sqrt($nspeed*$nspeed + $espeed*$espeed);
	print "$n\r\n";
	printTime $t;
	print " --> ";
	$t += 200;
	printTime $t;
	print "\r\n";
	printf "vspeed=%04.2f km/h\r\nhspeed=%04.2f km/h\r\nalt=%04.2f m\tglide=%04.2f\r\n\r\n", $vspeed*3.6, $hspeed*3.6, $hMsl, $hspeed/$vspeed;
	++$n;
}

# TODO: tee mingi spidoka moodi animatsiooni genereerija.