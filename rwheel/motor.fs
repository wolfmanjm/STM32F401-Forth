#require gpio-simple.fs
#require pwm.fs
#require qei.fs

PORTA 11 pin STBYpin
PORTA 10 pin IN1pin
PORTA  9 pin IN2pin
PORTA  8 pin buzzer

10 variable lowLimit

0 constant LOW
1 constant HIGH

: motor-init
	IN1pin output
	IN2pin output
	STBYpin output
	buzzer output
	PWM-init     		\ PA6
	0 pwm-duty_cycle_255
	true pwm-enable
	LOW IN1pin set  	\ PA10
	LOW IN2pin set  	\ PA9
	HIGH STBYpin set  	\ PA11
	LOW buzzer set  	\ PA8
	qei-init 			\ PA0 PA1
;

: M_FORWARD  LOW IN2pin set  HIGH IN1pin set ;
: M_REVERSE  LOW IN1pin set  HIGH IN2pin set ;
: M_STOP     LOW IN1pin set  LOW  IN2pin set ;

: run ( cmd -- )
    case
        1 of M_FORWARD endof
        0 of M_STOP    endof
       -1 of M_REVERSE endof
        ." Unknown motor movement"
    endcase
;

: map ( x inmin inmax outmin outmax -- y )
	\ return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
	2dup swap - >r				\ x inmin inmax outmin outmax R: outmax-outmin
	2swap 2dup swap - >r		\ x outmin outmax inmin inmax R: outmax-outmin inmax-inmin
	swap 4 pick swap - r> r>	\ x outmin outmax inmax x-inmin inmax-inmin outmax-outmin
	swap */						\ x outmin outmax inmax x-inmin*outmax-outmin/inmax-inmin
	nip nip + nip
;

: constrain ( n l u -- n ) rot 2dup < if drop nip else nip 2dup > if drop else nip then then ;
: setLowLimit ( i -- ) lowLimit ! ;
: getLowLimit ( -- i ) lowLimit @ ;

\ : set-speed ( pwm -- ) 0 255 getlowlimit 255 map getLowLimit 255 constrain pwm-duty_cycle_255 ;
: set-speed ( pwm -- ) getlowlimit 255 constrain pwm-duty_cycle_255 ;
: full-on 255 pwm-duty_cycle_255 ;
: full-off 0  pwm-duty_cycle_255 ;
: motor-standby ( flg -- ) if LOW else HIGH then STBYpin set ;

: set-motor ( pwm -- )
	dup abs set-speed
	dup 0< if M_REVERSE drop else
	    0= if M_STOP    else
			  M_FORWARD then then
;

: buzz ( ms -- ) HIGH buzzer set ms LOW buzzer set ;

\ encoder
12,0 100,37 f* 2constant PPR  \ 100.37 pulses per rev = counts/rev * gear ratio, 278 max RPM 1509 DPS. 100.37 gear ratio, 12CPR encoder

false variable rpm-dir
0 variable last-rpm
: rpm-init qei-get last-rpm ! ;
: get-rpm-dir rpm-dir @ ;

: get-rpm ( delayms -- rpm )
	\ ((float)cnt / PPR) / (delta / 60000.0) );
	last-rpm @ qei-get dup last-rpm !
	2dup < if swap - true else - false then rpm-dir !  \ save direction
	s>f PPR f/ 			\ rotations fractional
	rot s>f 60000,0 f/  \ delta time in seconds fractional
	f/ 0,5 f+ f>s		\ RPM round up and truncate
;

: test-rpm ( pwm -- )
	motor-init
	false motor-standby
	set-motor
	1000 ms    	\ give time to wind up
	rpm-init
	begin
		300 ms
		300 get-rpm . ." RPM" cr
 	key? until
	0 set-motor
	true motor-standby
;

: test-enc
	qei-init
	begin
		qei-get . cr
		200 ms
	key? until
;
