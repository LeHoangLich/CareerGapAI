import {it,expect} from 'vitest';
import {hasSkill,foldSkill} from './skillCatalog';
it('avoids exact aliases while retaining distinct skills and custom text',()=>{
 const react={id:'react',name:'React',category:'CNTT',aliases:['ReactJS','React.js']};
 expect(hasSkill(['React.js'],react)).toBe(true);expect(hasSkill(['Required: ReactJS'],react)).toBe(true);
 expect(hasSkill(['React Native'],react)).toBe(false);expect(foldSkill('Giao tiếp')).toBe('giao tiep');
 expect(hasSkill(['JavaScript'],{id:'java',name:'Java',category:'CNTT',aliases:[]})).toBe(false);
});
