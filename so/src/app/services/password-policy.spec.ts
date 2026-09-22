import { passwordAccepts } from './password-policy';
describe('Customer password policy',()=>{
 it('rejects short, common, sequential and repeated passwords',()=>{
  for(const value of ['ShortPass10','password1234','PASSWORD1234!','123456789012','abcdabcdabcd','aaaaaaaaaaaa'])expect(passwordAccepts(value)).toBe(false);
 });
 it('accepts long passphrases including Hebrew and spaces',()=>{
  expect(passwordAccepts('שלוש מילים שונות לקנייה')).toBe(true);
  expect(passwordAccepts('Fresh four words for SO 2026')).toBe(true);
 });
 it('enforces the maximum length',()=>expect(passwordAccepts('good long password '.repeat(30))).toBe(false));
});
