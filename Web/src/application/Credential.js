import { atom, useAtom } from "jotai"
import { Box, Button, Dialog, FormControlLabel, LinearProgress, Switch, Typography } from "@material-ui/core";
import { Formik, Form, Field } from 'formik';
import { TextField } from 'formik-material-ui';
import { delay } from "../common/utils";
import { useState } from "react";
import { setErrorMessage } from "../common/MessageSnackbar";
import { sha256 } from '../common/utils'

const credentialAtom = atom(true)
const usernameKey = 'credential.userName'

export const useCredential = () => useAtom(credentialAtom)

export default function Credential() {
    const [show, setShow] = useCredential()
    const [showConfirm, setShowConfirm] = useState(false)
    const userName = localStorage.getItem(usernameKey) ?? ''

    return (
        <Dialog open={show} onClose={() => { }}  >
            <Box style={{ width: 320, height: 330, padding: 20 }}>

                {!showConfirm && <Typography variant="h5" style={{ paddingBottom: 10 }} >Login</Typography>}
                {showConfirm && <Typography variant="h5" style={{ paddingBottom: 10 }} >Create an Account</Typography>}

                <Formik
                    initialValues={{
                        username: userName,
                        password: '',
                        confirm: '',
                        create: false
                    }}

                    validate={async values => {
                        const errors = {};
                        if (!values.username) {
                            errors.username = 'Required';
                        }
                        if (!values.password) {
                            errors.password = 'Required';
                        }
                        if (showConfirm && values.password !== values.confirm) {
                            errors.confirm = 'Does not match password'
                        }
                        return errors;
                    }}

                    onSubmit={async (values, { setSubmitting, setErrors, setValues }) => {
                        await delay(1000)
                        const sha = await sha256('text')
                        console.log('sha', 'text', sha)

                        localStorage.setItem(usernameKey, values.username)
                        setErrorMessage('Failed to login, invalid username or password')
                    }}
                >

                    {({ submitForm, isSubmitting }) => (
                        <Form>
                            {isSubmitting && <LinearProgress style={{ marginBottom: 10 }} />}
                            <Field
                                component={TextField}
                                name="username"
                                type="text"
                                label="Username"
                                fullWidth={true}
                            />
                            <br />
                            <Field
                                component={TextField}
                                type="password"
                                label="Password"
                                name="password"
                                fullWidth={true}
                            />
                            <br />
                            {showConfirm && <Field
                                component={TextField}
                                type="password"
                                label="Confirm"
                                name="confirm"
                                fullWidth={true}
                            />}

                            <br />
                            <FormControlLabel style={{ position: 'absolute', bottom: 70 }}
                                control={
                                    <Field component={Switch} type="checkbox" name="create" color='primary'
                                        onChange={e => setShowConfirm(e.target.checked)} />
                                }
                                label="Create an account"
                            />
                            <br />


                            <Box style={{ position: 'absolute', bottom: 15, left: 80 }}>
                                <Button
                                    variant="contained"
                                    color="primary"
                                    disabled={isSubmitting}
                                    onClick={submitForm}
                                    style={{ margin: 5, width: 80 }}
                                >OK</Button>

                                <Button
                                    variant="contained"
                                    color="primary"
                                    disabled={isSubmitting}
                                    onClick={() => setShow(false)}
                                    style={{ margin: 5, width: 85 }}
                                >Cancel</Button>
                            </Box>

                        </Form>
                    )}
                </Formik>
            </Box>
        </Dialog >
    )
}