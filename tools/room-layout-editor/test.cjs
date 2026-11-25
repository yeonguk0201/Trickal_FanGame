// Run: node test.cjs <path to playwright module>. Uses an isolated headless Edge context.
const { chromium } = require(process.argv[2] || 'playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
const assert = require('node:assert/strict');
(async () => {
 const browser = await chromium.launch({channel:'msedge',headless:true});
 const context = await browser.newContext({viewport:{width:1500,height:1050}});
 const page = await context.newPage();
 const errors=[]; page.on('pageerror',e=>errors.push(e.message));
 page.on('dialog',d=>d.accept());
 const target=path.resolve(__dirname,'../../tmp/room-layout-editor.html');
 await page.goto(pathToFileURL(target).href);
 await page.waitForSelector('.cell');
 const initial=await page.locator('.room').count();assert(initial>=30);
 const cell=(r,c)=>page.locator(`.cell[data-r="${r-1}"][data-c="${c-1}"]`);
 const grid=()=>page.evaluate(()=>room().grid.join('\n'));
 for(const [profile,count] of [['small',50],['basic',91],['wide',147],['tall',143],['large',231]]){
   await page.click('#new');await page.selectOption('#newProfile',profile);await page.fill('#newSlug','test-'+profile);await page.uncheck('#newSpawns');await page.click('#create');
   assert.equal(await page.locator('.cell').count(),count);
 }
 await page.locator('[data-symbol="#"]').click();
 await cell(1,11).click();assert.equal(await cell(1,11).textContent(),''); // Door protection.
 await cell(4,5).click();assert.equal(await cell(4,5).textContent(),'#');
 await page.click('#undo');assert.equal(await cell(4,5).textContent(),'');
 await page.click('#redo');assert.equal(await cell(4,5).textContent(),'#');
 await page.click('[data-tool="move"]');
 let a=await cell(4,5).boundingBox(),b=await cell(4,7).boundingBox();
 await page.mouse.move(a.x+a.width/2,a.y+a.height/2);await page.mouse.down();await page.mouse.move(b.x+b.width/2,b.y+b.height/2,{steps:8});await page.mouse.up();
 assert.equal(await cell(4,5).textContent(),'');assert.equal(await cell(4,7).textContent(),'#');
 await page.keyboard.press('ArrowRight');assert.equal(await cell(4,8).textContent(),'#');
 await page.fill('#selRow','5');await page.fill('#selCol','8');await page.click('#moveNumeric');assert.equal(await cell(5,8).textContent(),'#');
 await page.locator('[data-symbol="1"]').click();await cell(4,10).click();await cell(4,12).click();
 assert.equal(await cell(4,10).textContent(),'');assert.equal(await cell(4,12).textContent(),'1');
 await page.locator('[data-symbol="#"]').click();await cell(4,13).click();assert.equal(await cell(4,13).textContent(),''); // Right of spawn forbidden.
 await page.locator('[data-symbol="a"]').click();await cell(2,11).click();assert.equal(await cell(2,11).textContent(),'');
 await page.click('[data-tool="rect"]');
 a=await cell(7,4).boundingBox();b=await cell(8,6).boundingBox();
 await page.mouse.move(a.x+a.width/2,a.y+a.height/2);await page.mouse.down();await page.mouse.move(b.x+b.width/2,b.y+b.height/2,{steps:5});await page.mouse.up();
 assert.equal(await cell(8,6).textContent(),'a');assert.equal(await cell(7,4).textContent(),'a');
 await page.selectOption('#zoom','24');await page.click('[data-tool="move"]');
 a=await cell(5,8).boundingBox();b=await cell(5,9).boundingBox();await page.mouse.move(a.x+a.width/2,a.y+a.height/2);await page.mouse.down();await page.mouse.move(b.x+b.width/2,b.y+b.height/2,{steps:4});await page.mouse.up();assert.equal(await cell(5,9).textContent(),'#');
 const before=await grid();await page.reload();assert.equal(await grid(),before);
 await page.click('#export');const text=await page.inputValue('#textValue');assert(text.includes('id: large-test-large'));await page.locator('#textDialog [data-close]').click();
 await page.click('#backup');const json=await page.inputValue('#textValue');assert.equal(JSON.parse(json).version,1);await page.locator('#textDialog [data-close]').click();
 await page.click('#reset');assert.notEqual(await grid(),before);await page.click('#undo');assert.equal(await grid(),before);
 await page.click('#import');await page.fill('#textValue',json);await page.click('#applyText');assert.equal(await grid(),before);
 await page.click('#import');await page.fill('#textValue',text.replace('large-test-large','large-import-roundtrip'));await page.click('#applyText');assert.equal(await grid(),before);
 await page.click('#export');const [download]=await Promise.all([page.waitForEvent('download'),page.click('#download')]);assert.equal(download.suggestedFilename(),'large-import-roundtrip.room.txt');await page.locator('#textDialog [data-close]').click();
 await page.click('#import');await page.fill('#textValue','{"version":1,"rooms":[]}');await page.click('#applyText');assert(await page.locator('#textDialog').isVisible());await page.locator('#textDialog [data-close]').click();
 await page.click('#duplicate');assert((await page.inputValue('#idField')).includes('-copy'));await page.click('#delete');await page.click('#undo');assert((await page.inputValue('#idField')).includes('-copy'));
 // Full-height barriers: destructible/timed routes pass; permanent hazards remain blocking.
 await page.click('#new');await page.selectOption('#newProfile','large');await page.fill('#newSlug','barrier-rules');await page.uncheck('#newSpawns');await page.click('#create');
 for(const symbol of ['#','*','P','Z','a','b','~','w','m','^','T','o']){
   await page.locator(`[data-symbol="${symbol}"]`).click();await page.click('[data-tool="rect"]');
   a=await cell(1,7).boundingBox();b=await cell(11,7).boundingBox();
   await page.mouse.move(a.x+a.width/2,a.y+a.height/2);await page.mouse.down();await page.mouse.move(b.x+b.width/2,b.y+b.height/2,{steps:12});await page.mouse.up();
   await page.waitForTimeout(80);
   const disconnected=await page.locator('#issues').innerText();
   assert.equal(disconnected.includes('문 사이가 연결되지'),['^','T','o'].includes(symbol),symbol);
 }
 // Layout-Wave: no waves keeps the old rule; waves can be auto or fixed, export/import round-trips, checks run.
 await page.click('#new');await page.selectOption('#newProfile','large');await page.fill('#newSlug','wave-plan');await page.click('#create');
 assert((await page.inputValue('#textValue').catch(()=>''))!==null);
 assert(!(await page.evaluate(()=>serialize(room()))).includes('wave1'));
 await page.click('#addWave');assert.equal(await page.locator('.wave').count(),2);
 await page.selectOption('.wave select[data-wave="1"]','fixed');
 assert.equal(await page.evaluate(()=>waveText(room().waves[1])),'chaser*2');
 await page.locator('.wave').nth(1).locator('input[type=number]').fill('3');await page.locator('.wave').nth(1).locator('input[type=number]').dispatchEvent('change');
 await page.locator('.wave').nth(1).getByRole('button',{name:'＋ 몬스터'}).click();
 await page.locator('.wave').nth(1).getByRole('button',{name:'또는'}).nth(1).click();
 const planned=await page.evaluate(()=>serialize(room()));
 assert(planned.includes('wave1: auto'+String.fromCharCode(10)+'wave2: chaser*3, sniper|chaser'+String.fromCharCode(10)),planned);
 await page.click('#import');await page.fill('#textValue',planned.replace('large-wave-plan','large-wave-copy'));await page.click('#applyText');
 assert.equal(await page.evaluate(()=>room().waves.map(waveText).join(' / ')),'auto / chaser*3, sniper|chaser');
 await page.locator('.wave').nth(1).locator('input[type=number]').first().fill('12');await page.locator('.wave').nth(1).locator('input[type=number]').first().dispatchEvent('change');
 await page.waitForTimeout(80);assert((await page.locator('#issues').innerText()).includes('스폰 자리 11개보다 많습니다'));
 await page.selectOption('.wave select[data-wave="1"]','auto');await page.click('#clearWaves');
 assert.equal(await page.evaluate(()=>room().waves.length),0);await page.click('#undo');assert.equal(await page.evaluate(()=>room().waves.length),2);
 await page.click('#import');await page.fill('#textValue',planned.replace('wave1: auto','wave1: dragon*2'));await page.click('#applyText');assert(await page.locator('#textDialog').isVisible());await page.locator('#textDialog [data-close]').click();
 // Layout-Wave-2: an entry's variant and spawn numbers, the board highlight, guards and the text round-trip.
 await page.selectOption('.wave select[data-wave="1"]','fixed');
 const second=page.locator('.wave').nth(1);
 await second.locator('.entry .variant').selectOption('elite');
 await second.locator('.entry .points').fill('1/2/4');await second.locator('.entry .points').dispatchEvent('change');
 assert.equal(await page.evaluate(()=>waveText(room().waves[1])),'chaser*2!elite@1/2/4');
 assert.equal(await page.locator('.wave').nth(1).locator('.entry .variant option[value="elite"]').count(),1);
 await page.locator('.wave').nth(1).locator('.entry').first().hover();
 const expectedPins=await page.evaluate(()=>{const g=room().grid.join('');return [1,2,4].reduce((n,p)=>n+[...g].filter(ch=>ch===String(p)).length,0)});
 assert.equal(await page.locator('.cell.pinned').count(),expectedPins);
 await page.locator('.wave').nth(1).locator('.entry .points').fill('1');await page.locator('.wave').nth(1).locator('.entry .points').dispatchEvent('change');
 assert.equal(await page.evaluate(()=>waveText(room().waves[1])),'chaser*2!elite@1/2/4');
 await page.locator('.wave').nth(1).getByRole('button',{name:'또는'}).click();
 await page.locator('.wave').nth(1).locator('.entry select').nth(1).selectOption('jyubi');
 assert.equal(await page.evaluate(()=>waveText(room().waves[1])),'chaser|fast-chaser*2!elite@1/2/4');
 const pinned=(await page.evaluate(()=>serialize(room()))).replace(/wave2: .*/,'wave2: sniper*2!champion-shots@5/6, chaser!normal').replace('large-wave-copy','large-wave-pins');
 await page.click('#import');await page.fill('#textValue',pinned);await page.click('#applyText');
 assert.equal(await page.evaluate(()=>room().waves.map(waveText).join(' / ')),'auto / sniper*2!champion-shots@5/6, chaser!normal');
 for(const broken of ['jyubi!elite','chaser*3@1/2','self-destruct!champion-blast','chaser!king','chaser@9']){await page.click('#import');await page.fill('#textValue',pinned.replace(/wave2: .*/,'wave2: '+broken));await page.click('#applyText');assert(await page.locator('#textDialog').isVisible(),broken);await page.locator('#textDialog [data-close]').click()}
 // Clean visual review of a built-in layout in the isolated test browser.
 await page.fill('#search','large-clockwork-carpet');await page.locator('.room').click();await page.fill('#search','');await page.selectOption('#zoom','32');await page.screenshot({path:path.resolve(__dirname,'../../tmp/room-layout-editor-preview.png'),fullPage:true});
 assert.deepEqual(errors,[]);console.log(`PASS: ${initial} rooms; 5 sizes, placement guards, drag/move/zoom, undo/redo, rectangle, reload, reset, JSON/text round-trip, download, malformed import, duplicate/delete, layout waves, wave variants and spawn pins.`);
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
