import {normalizePhone,displayPhone} from './israeli-phone';
describe('Israeli phone validation',()=>{
 it.each(['0501234567','050-123-4567','050 123 4567','+972501234567','+972 50-123-4567'])('normalizes supported mobile format %s',value=>expect(normalizePhone(value)).toBe('0501234567'));
 it.each(['','050123456','05012345678','050123456789999999999','501234567','+9720501234567','+1501234567','050ABC4567','0501234567x','0501234567+','0001234567'])('rejects malformed or incorrect length %s',value=>expect(normalizePhone(value)).toBeNull());
 it('supports landlines and internet phone lengths',()=>{expect(normalizePhone('02-123-4567')).toBe('021234567');expect(normalizePhone('072-123-4567')).toBe('0721234567');expect(normalizePhone('02-123-45678')).toBeNull();});
 it('formats mobile and landline without hiding invalid input',()=>{expect(displayPhone('+972501234567')).toBe('050-123-4567');expect(displayPhone('021234567')).toBe('02-123-4567');expect(displayPhone('05012345678')).toBe('05012345678');});
});