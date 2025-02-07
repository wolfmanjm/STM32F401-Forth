\ uart is main port anyway

: uart-puts type ;
: uart-putc emit ;
: uart-cr cr ;
: uart-rx-ready key? ;
: uart-get key ;
: uart-flush begin uart-rx-ready while uart-get drop repeat ;

: inc-dec ( cmd f dvar -- )
    3 pick [char] + = if f+! else
    3 pick [char] - = if -rot dnegate rot f+! else
    drop 2drop
    then then
    drop
;

: f..3>s ( f -- ) tuck dabs 0 <# #s 2drop [char] . hold< f# f# f# drop sign 0 0 #> ;

: print-values
    s" K1: " uart-puts K1Gain 2@ fr3 f..3>s uart-puts uart-cr
    s" K2: " uart-puts K2Gain 2@ fr3 f..3>s uart-puts uart-cr
    s" K3: " uart-puts K3Gain 2@ fr3 f..3>s uart-puts uart-cr
    s" K4: " uart-puts K4Gain 2@ fr3 f..3>s uart-puts uart-cr
;

: calibrate-set ( cmd -- )
    dup char + = calibrating @ 0= and if drop true calibrating ! s" calibrating on" uart-puts uart-cr exit then
    char - = calibrating @ 0<> and if
        s" calibrating off" uart-puts uart-cr

    then

      \ if (cmd == '+' && !calibrating) {
      \   calibrating = true;
      \    Serial.println("calibrating on");
      \ }
      \ if (cmd == '-' && calibrating)  {
      \   Serial.println("calibrating off");
      \   Serial.print("X: "); Serial.print(AcX + 16384); Serial.print(" Y: "); Serial.println(AcY);
      \   if (abs(AcY) < 3000) {
      \     offsets.ID = 78;
      \     offsets.X = AcX + 16384;
      \     offsets.Y = AcY;
      \     digitalWrite(BUZZER, HIGH);
      \     delay(70);
      \     digitalWrite(BUZZER, LOW);
      \     EEPROM.put(0, offsets);
      \     calibrating = false;
      \     calibrated = true;
      \   } else {
      \     Serial.println("The angle are wrong!!!");
      \     calibrating = false;
      \     digitalWrite(BUZZER, HIGH);
      \     delay(50);
      \     digitalWrite(BUZZER, LOW);
      \     delay(70);
      \     digitalWrite(BUZZER, HIGH);
      \     delay(50);
      \     digitalWrite(BUZZER, LOW);
      \   }
      \ }
;

: console
    uart-rx-ready not if exit then
    uart-get        \ param
    2 ms
    uart-rx-ready false = if drop exit then
    uart-get        \ cmd
    uart-flush
    swap
    case
        [char] i of 0,5   K2Gain inc-dec print-values endof
        [char] p of 1,0   K1Gain inc-dec print-values endof
        [char] s of 1,0   K4Gain inc-dec print-values endof
        [char] a of 0,005 K3Gain inc-dec print-values endof
        [char] c of calibrate-set  endof
        s" Unknown command: " uart-puts uart-putc
    endcase
;

: test-console
	begin console key? until
;
